using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DeskPet.Services;

namespace DeskPet.Shell;

public partial class PetWindow : Window
{
    public static PetWindow Instance { get; } = new();

    private const double PanelHeight = 78;
    private const double DragThreshold = 3;

    private Point _pressPoint;
    private Point _pressScreen;
    private DateTime _pressTime;
    private bool _pressActive;
    private bool _dragging;
    private bool _panelVisible;
    private bool _shown;

    private PetWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        PetManager.Instance.PropertyChanged += OnPetChanged;
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
        }
    }

    private void ApplySize()
    {
        var size = PetManager.Instance.WindowSize;
        Width = size.Width;
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
        _pressTime = DateTime.Now;
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
        if (_pressTime != DateTime.MinValue && (now - _pressTime).TotalSeconds < 0.4)
        {
            // double-click: go home / leave home
            HidePanel();
            if (PetManager.Instance.LifeState == Models.PetLifeState.AtHome)
                PetManager.Instance.LeaveHome();
            else
                PetManager.Instance.GoHome();
            _pressTime = DateTime.MinValue;
        }
        else
        {
            _pressTime = now;
            if (_panelVisible) HidePanel();
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
