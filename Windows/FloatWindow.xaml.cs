using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskPet.Models;
using DeskPet.Services;

namespace DeskPet.Shell;

/// <summary>
/// A small draggable circular floating window that replaces the island "notch".
/// Clicking it opens a sub-menu (context menu) holding every action: go home /
/// out, AI chat, settings, model switching, skin import, autostart toggle,
/// restart, quit.
/// </summary>
public partial class FloatWindow : Window
{
    public static FloatWindow Instance { get; } = new();

    private bool _dragging;
    private Point _downScreen;
    private double _dragDistance;

    private FloatWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PositionDefault();
            RefreshFace();
        };
        SourceInitialized += (_, _) => NativeWindow.HideFromAltTab(this);   // 不显示在 Alt+Tab / 任务栏
        // Bind to the window itself so the whole (mostly transparent) window area
        // participates in drag + click, not just the small circle border.
        MouseLeftButtonDown += FloatWindow_MouseDown;
        MouseMove += FloatWindow_MouseMove;
        MouseLeftButtonUp += FloatWindow_MouseUp;
        PetManager.Instance.PropertyChanged += OnPetChanged;
    }

    private void OnPetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PetManager.CurrentFrame)) RefreshFace();
    }

    private void PositionDefault()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 20;
        Top = wa.Top + 40;
        SyncHomeAnchor();
    }

    /// <summary>Apply the configured float-window size + opacity.
    /// The whole window resizes (Viewbox scales the circle uniformly), so there
    /// is no clipped "square box" cutting off the circle when scaled up.</summary>
    public void ApplyAppearance()
    {
        double s = Math.Clamp(AppSettings.Instance.FloatScale, 0.5, 2.5);
        double o = Math.Clamp(AppSettings.Instance.FloatOpacity, 0.2, 1.0);
        Width = 64 * s;
        Height = 64 * s;
        Opacity = o;
        SyncHomeAnchor();
    }

    /// <summary>Tell the pet manager where "home" is (the floating window center),
    /// so going home walks to the floating window, not the tray.</summary>
    private void SyncHomeAnchor()
    {
        try
        {
            PetManager.HomeAnchor = new Point(Left + Width / 2, Top + Height / 2);
        }
        catch { }
    }

    public void ShowFloat()
    {
        if (!IsVisible) base.Show();
        SetAsTopmost();
        ApplyAppearance();
        RefreshFace();
    }

    private void SetAsTopmost()
    {
        Topmost = true;
    }

    private void RefreshFace()
    {
        Dispatcher.Invoke(() =>
        {
            var frame = PetManager.Instance.CurrentFrame;
            if (frame != null)
            {
                FaceImage.Source = frame;
                IconFallback.Visibility = Visibility.Collapsed;
                FaceImage.Visibility = Visibility.Visible;
            }
            else
            {
                IconFallback.Visibility = Visibility.Visible;
                FaceImage.Visibility = Visibility.Collapsed;
            }
        });
    }

    // ---- Dragging ----

    private const double DragThreshold = 5;

    private void FloatWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;          // not dragging until we move past threshold
        _downScreen = PointToScreen(e.GetPosition(this));
        _dragDistance = 0;
        CaptureMouse();
        e.Handled = true;
    }

    private void FloatWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var curScreen = PointToScreen(e.GetPosition(this));
        _dragDistance += Math.Abs(curScreen.X - _downScreen.X) + Math.Abs(curScreen.Y - _downScreen.Y);

        if (_dragDistance > DragThreshold)
        {
            _dragging = true;
            Left += curScreen.X - _downScreen.X;
            Top += curScreen.Y - _downScreen.Y;
            SyncHomeAnchor();
        }
        _downScreen = curScreen;
        e.Handled = true;
    }

    private void FloatWindow_MouseUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDragging = _dragging;
        _dragging = false;
        ReleaseMouseCapture();

        if (!wasDragging)
        {
            // Click (no meaningful movement) → open the sub-menu. Defer opening
            // so the current mouse-up completes first; opening a ContextMenu
            // synchronously inside MouseUp can get it dismissed immediately.
            var cb = CircleButton;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                OpenMenuAt(cb);
            }));
        }
        e.Handled = true;
    }

    private void OpenMenuAt(FrameworkElement target)
    {
        var menu = BuildMenu();
        menu.PlacementTarget = target;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.HorizontalOffset = 0;
        menu.VerticalOffset = 4;
        menu.StaysOpen = false;
        menu.IsOpen = true;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Clear();

        // state-dependent home/out
        bool atHome = PetManager.Instance.LifeState == PetLifeState.AtHome;
        var homeOut = new MenuItem
        {
            Header = atHome ? "出巢（回到桌面）" : "回巢（回到悬浮窗）",
            Tag = "homeout",
        };
        homeOut.Click += (_, _) =>
        {
            if (PetManager.Instance.LifeState == PetLifeState.AtHome)
                PetManager.Instance.LeaveHome();
            else
                PetManager.Instance.GoHome();
        };
        menu.Items.Add(homeOut);

        var chat = new MenuItem { Header = "AI 对话" };
        chat.Click += (_, _) => ChatWindow.ShowChat();
        menu.Items.Add(chat);

        var settings = new MenuItem { Header = "设置" };
        settings.Click += (_, _) => ((App)Application.Current).OpenSettings();
        menu.Items.Add(settings);

        // ---- 音乐控制 (SMTC) ----
        var musicTitle = new MenuItem { Header = "🎵 音乐控制" };
        musicTitle.IsEnabled = false;
        menu.Items.Add(musicTitle);

        var mc = MediaController.Instance;
        var playPause = new MenuItem { Header = mc.IsPlaying ? "暂停播放" : "播放/暂停" };
        playPause.Click += async (_, _) => await mc.PlayPauseAsync();
        menu.Items.Add(playPause);

        var prev = new MenuItem { Header = "上一首" };
        prev.Click += async (_, _) => await mc.PreviousAsync();
        menu.Items.Add(prev);

        var next = new MenuItem { Header = "下一首" };
        next.Click += async (_, _) => await mc.NextAsync();
        menu.Items.Add(next);

        if (!string.IsNullOrEmpty(mc.Title) && mc.Title != "DeskPet")
        {
            var nowPlaying = new MenuItem { Header = $"{Truncate(mc.Title, 20)} - {Truncate(mc.Artist, 16)}" };
            nowPlaying.IsEnabled = false;
            menu.Items.Add(nowPlaying);
        }

        // ---- 音量 ----
        var volTitle = new MenuItem { Header = $"🔊 音量 {Math.Round(VolumeController.Get() * 100)}%" };
        volTitle.IsEnabled = false;
        menu.Items.Add(volTitle);
        var volUp = new MenuItem { Header = "音量 +" };
        volUp.Click += (_, _) => VolumeController.Set(VolumeController.Get() + 0.08f);
        menu.Items.Add(volUp);
        var volDown = new MenuItem { Header = "音量 -" };
        volDown.Click += (_, _) => VolumeController.Set(VolumeController.Get() - 0.08f);
        menu.Items.Add(volDown);
        var mute = new MenuItem { Header = "静音切换" };
        mute.Click += (_, _) => VolumeController.ToggleMute();
        menu.Items.Add(mute);

        // ---- 电池 ----
        var b = BatteryService.Instance;
        var bat = new MenuItem
        {
            Header = b.Charging
                ? $"🔋 {b.Level}%（充电中）"
                : b.Plugged
                    ? $"🔌 {b.Level}%（电源）"
                    : $"🔋 {b.Level}%（电池）",
        };
        bat.IsEnabled = false;
        menu.Items.Add(bat);

        // ---- 宠物互动 ----
        var poke = new MenuItem { Header = "戳一戳宠物" };
        poke.Click += (_, _) => PetManager.Instance.Poke();
        menu.Items.Add(poke);
        var petIt = new MenuItem { Header = "抚摸宠物" };
        petIt.Click += (_, _) => PetManager.Instance.Pet();
        menu.Items.Add(petIt);

        menu.Items.Add(new Separator());

        // Mode switch submenu
        var models = new MenuItem { Header = "切换宠物形象" };
        foreach (var m in PetSkin.GetAvailableModels())
        {
            var mi = new MenuItem { Header = m, Tag = m, IsCheckable = true };
            mi.IsChecked = m == AppSettings.Instance.PetModel;
            mi.Click += (_, _5) => PetManager.Instance.SwitchModel(m);
            models.Items.Add(mi);
        }
        menu.Items.Add(models);

        var scale = new MenuItem { Header = "宠物大小" };
        var sm = new MenuItem { Header = "小" }; sm.Click += (_, _2) => SetScale(0.5); scale.Items.Add(sm);
        var mm = new MenuItem { Header = "中" }; mm.Click += (_, _3) => SetScale(0.75); scale.Items.Add(mm);
        var lm = new MenuItem { Header = "大" }; lm.Click += (_, _4) => SetScale(1.2); scale.Items.Add(lm);
        menu.Items.Add(scale);

        menu.Items.Add(new Separator());

        var autostart = new MenuItem { Header = "开机自启动", IsCheckable = true };
        autostart.IsChecked = AutoStart.IsEnabled;
        autostart.Click += (_, _) =>
        {
            bool on = !AutoStart.IsEnabled;
            AutoStart.SetEnabled(on);
            autostart.IsChecked = on;
        };
        menu.Items.Add(autostart);

        var clickThrough = new MenuItem { Header = "宠物鼠标穿透（点不到宠物）", IsCheckable = true };
        clickThrough.IsChecked = AppSettings.Instance.PetClickThrough;
        clickThrough.Click += (_, _) =>
        {
            AppSettings.Instance.PetClickThrough = !AppSettings.Instance.PetClickThrough;
            AppSettings.Instance.Save();
            clickThrough.IsChecked = AppSettings.Instance.PetClickThrough;
            PetWindow.NotifyClickThroughChanged();   // 立即应用到桌宠窗口
        };
        menu.Items.Add(clickThrough);

        menu.Items.Add(new Separator());

        var restart = new MenuItem { Header = "重启 DeskPet" };
        restart.Click += (_, _) => ((App)Application.Current).Restart();
        menu.Items.Add(restart);

        var quit = new MenuItem { Header = "退出" };
        quit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quit);

        return menu;
    }

    private static void SetScale(double v)
    {
        AppSettings.Instance.PetScale = v;
        AppSettings.Instance.Save();
        PetManager.Instance.ApplyScale();
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}
