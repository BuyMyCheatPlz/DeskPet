using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeskPet.Models;
using DeskPet.Services;

namespace DeskPet.Shell;

public partial class PetWindow : Window
{
    public static PetWindow Instance { get; } = new();

    private const double PanelHeight = 110;
    private const double MinPanelWidth = 160;
    private const double DragThreshold = 3;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;

    private Point _pressPoint;
    private Point _pressScreen;
    private DateTime _lastClickTime = DateTime.MinValue;
    private bool _pressActive;
    private bool _dragging;
    private bool _panelVisible;
    private bool _shown;
    private System.Windows.Threading.DispatcherTimer? _cursorTimer;
    private System.Windows.Threading.DispatcherTimer? _speechTimer;

    private PetWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        PetManager.Instance.PropertyChanged += OnPetChanged;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            NativeWindow.HideFromAltTab(this);   // 不显示在 Alt+Tab / 任务栏
            ApplyClickThrough();                 // 应用鼠标穿透扩展样式
        };

        // Poll the cursor while in click-through so the pet clears the mouse
        // instead of sitting under it. ~16ms to react quickly during fast mouse
        // movement (e.g. games).
        _cursorTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _cursorTimer.Tick += (_, _) => CursorSweepTick();
        _cursorTimer.Start();
    }

    /// <summary>When click-through is enabled and the cursor is over (or near) the
    /// model, tell the pet to walk away from the mouse immediately and fast.
    /// Stops as soon as the cursor leaves the padded area.</summary>
    private void CursorSweepTick()
    {
        if (!AppSettings.Instance.PetClickThrough) return;
        if (!IsVisible || PetManager.Instance.LifeState != PetLifeState.Roaming) return;

        // Use Win32 GetCursorPos (physical px) converted to DIP so it matches
        // WindowPosition's coordinate space. Mouse.GetPosition(null) returns
        // (0,0) on this transparent window, which makes the hit test never fire.
        bool ok = NativeWindow.TryGetCursorPos(out int px, out int py);
        double sx = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double sy = VisualTreeHelper.GetDpi(this).DpiScaleY;
        var cursor = ok ? new Point(px / sx, py / sy) : new Point(0, 0);
        var pet = PetManager.Instance;
        var size = pet.WindowSize;
        // Model area in screen coords padded ~50px so the pet dodges early,
        // before a fast-moving cursor actually lands on it.
        double pad = 50;
        var rect = new System.Windows.Rect(
            pet.WindowPosition.X - pad,
            pet.WindowPosition.Y - pad,
            Math.Max(size.Width, 160) + pad * 2,
            size.Height + pad * 2);
        if (rect.Contains(cursor))
        {
            pet.FleeFromMouse(cursor);
        }
        else
        {
            pet.CancelFlee();
        }
    }

    private void ApplyClickThrough()
    {
        NativeWindow.SetClickThrough(this, AppSettings.Instance.PetClickThrough);
    }

    /// <summary>When click-through is enabled, make the pet window transparent to
    /// the mouse: clicks pass through to whatever is behind the pet.</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmNcHitTest)
        {
            // Keep the extended style in sync so toggles apply immediately.
            ApplyClickThrough();
            if (AppSettings.Instance.PetClickThrough)
            {
                handled = true;
                return (IntPtr)HtTransparent;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Show a speech bubble above the pet's head (used for AI replies).
    /// Auto-hides after a few seconds; calling again refreshes the text and timer.</summary>
    public static void ShowSpeechBubble(string text)
    {
        Instance.ShowSpeechBubbleCore(text);
    }

    private void ShowSpeechBubbleCore(string text)
    {
        Dispatcher.Invoke(() =>
        {
            SpeechText.Text = text ?? "";
            SpeechBubble.Visibility = Visibility.Visible;
            if (_speechTimer == null)
            {
                _speechTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(6),
                };
                _speechTimer.Tick += (_, _) =>
                {
                    _speechTimer.Stop();
                    SpeechBubble.Visibility = Visibility.Collapsed;
                };
            }
            _speechTimer.Stop();
            _speechTimer.Start();
        });
    }

    // Re-apply the click-through style once the extra style has been set, so
    // clicks actually land on the window behind the pet.
    public static void NotifyClickThroughChanged()
    {
        if (Instance.IsLoaded)
            Instance.ApplyClickThrough();
    }

    public void ShowPet()
    {
        if (_shown) return;
        _shown = true;
        base.Show();
        ApplySize();
        UpdatePosition();
        UpdateAlpha();
        PetManager.Instance.Start();
    }

    private void OnPetChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PetManager.CurrentFrame):
                Dispatcher.Invoke(() =>
                {
                    FrameImage.Source = PetManager.Instance.CurrentFrame;
                    FrameImage.RenderTransform = new ScaleTransform(PetManager.Instance.FacingLeft ? -1 : 1, 1);
                });
                break;
            case nameof(PetManager.WindowPosition):
                Dispatcher.Invoke(UpdatePosition);
                break;
            case nameof(PetManager.WindowAlpha):
                Dispatcher.Invoke(UpdateAlpha);
                break;
            case nameof(PetManager.LifeState):
                Dispatcher.Invoke(UpdateAlpha);
                break;
            case nameof(PetManager.WindowSize):
                Dispatcher.Invoke(() =>
                {
                    ApplySize();
                    UpdatePosition();
                });
                break;
        }
    }

    private void ApplySize()
    {
        var size = PetManager.Instance.WindowSize;
        Width = Math.Max(size.Width, MinPanelWidth);
        Height = size.Height + PanelHeight;
    }

    private void UpdatePosition()
    {
        var pos = PetManager.Instance.WindowPosition;
        Left = pos.X;
        Top = pos.Y - PanelHeight;
    }

    private void UpdateAlpha()
    {
        var alpha = PetManager.Instance.WindowAlpha;
        Opacity = Math.Clamp(alpha, 0, 1);
        bool atHome = PetManager.Instance.LifeState == Models.PetLifeState.AtHome;
        if (atHome)
        {
            if (IsVisible) Hide();
        }
        else if (!IsVisible)
        {
            base.Show();
        }
    }

    // ---- Mouse interactions ----

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressActive = true;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _pressScreen = Mouse.GetPosition(null);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressActive) return;
        var current = e.GetPosition(this);
        double dist = Math.Sqrt(Math.Pow(current.X - _pressPoint.X, 2) + Math.Pow(current.Y - _pressPoint.Y, 2));

        if (!_dragging && dist > DragThreshold)
        {
            _dragging = true;
            PetManager.Instance.StartDrag();
            HidePanel();
        }

        if (_dragging)
        {
            var mouse = Mouse.GetPosition(null);
            double offsetX = _pressScreen.X - Left;
            double offsetY = _pressScreen.Y - Top;
            Left = mouse.X - offsetX;
            Top = mouse.Y - offsetY;
            PetManager.Instance.SyncWindowPosition(new Point(Left, Top + PanelHeight));
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressActive) return;
        _pressActive = false;
        ReleaseMouseCapture();

        if (_dragging)
        {
            _dragging = false;
            PetManager.Instance.SyncWindowPosition(new Point(Left, Top + PanelHeight));
            PetManager.Instance.EndDrag();
            return;
        }

        HandleClick();
    }

    private void HandleClick()
    {
        var now = DateTime.Now;
        bool isDouble = _lastClickTime != DateTime.MinValue
                        && (now - _lastClickTime).TotalMilliseconds < 400;

        if (isDouble)
        {
            // Double click → go home / leave home.
            _lastClickTime = DateTime.MinValue;
            HidePanel();
            if (PetManager.Instance.LifeState == Models.PetLifeState.AtHome)
                PetManager.Instance.LeaveHome();
            else
                PetManager.Instance.GoHome();
        }
        else
        {
            // Single click → interact (poke) + open the action panel.
            _lastClickTime = now;
            if (_panelVisible)
            {
                HidePanel();
            }
            else
            {
                PetManager.Instance.Poke();
                ShowPanel();
            }
        }
    }

    private void ShowPanel()
    {
        _panelVisible = true;
        ActionPanel.Visibility = Visibility.Visible;
        UpdateStats();
    }

    private void HidePanel()
    {
        _panelVisible = false;
        ActionPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateStats()
    {
        double mood = PetManager.Instance.Happiness;
        string moodEmoji = mood > 70 ? "😺" : mood > 40 ? "🙂" : mood > 20 ? "😿" : "😾";
        string heart = mood > 80 ? "❤️" : mood > 50 ? "💛" : mood > 20 ? "🧡" : "💔";
        StatsText.Text = $"{moodEmoji} {heart} {(int)mood}%  ✨ {(int)PetManager.Instance.Cleanliness}%";
    }

    private void PetButton_Click(object sender, RoutedEventArgs e)
    {
        PetManager.Instance.Pet();
        HidePanel();
    }

    private void ChatButton_Click(object sender, RoutedEventArgs e)
    {
        HidePanel();
        ChatWindow.ShowChat();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        PetManager.Instance.GoHome();
        HidePanel();
    }
}
