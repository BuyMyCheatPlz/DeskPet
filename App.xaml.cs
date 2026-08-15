using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using DeskPet.Models;
using DeskPet.Services;
using DeskPet.Shell;

namespace DeskPet;

/// <summary>
/// Application entry point. Owns the tray icon and coordinates the two overlay
/// windows (desktop pet + dynamic island) and the settings window.
/// </summary>
public partial class App : System.Windows.Application
{
    private NotifyIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure the default skin folder exists with a built-in placeholder skin
        PetSkin.EnsureDefaultSkin();

        AppSettings.Instance.Load();

        CreateTrayIcon();

        // Show the desktop pet (start the pet mid-desktop).
        if (AppSettings.Instance.PetEnabled)
        {
            PetWindow.Instance.ShowPet();
        }

        if (AppSettings.Instance.FirstLaunch)
        {
            AppSettings.Instance.FirstLaunch = false;
            AppSettings.Instance.Save();
            PlayWelcomeSound();
        }
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "DeskPet",
            Visible = true,
            Icon = BuildIcon(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Chat with pet", null, (_, _) => Dispatcher.Invoke(ChatWindow.ShowChat));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Let pet out", null, (_, _) => Dispatcher.Invoke(() => PetManager.Instance.LeaveHome()));
        menu.Items.Add("Send pet home", null, (_, _) => Dispatcher.Invoke(() => PetManager.Instance.GoHome()));

        // Pet model submenu
        var modelMenu = new ToolStripMenuItem("Pet model");
        foreach (var model in PetSkin.GetAvailableModels())
        {
            var item = new ToolStripMenuItem(model) { Checked = model == AppSettings.Instance.PetModel };
            item.Click += (_, _) => Dispatcher.Invoke(() => PetManager.Instance.SwitchModel(model));
            modelMenu.DropDownItems.Add(item);
        }
        modelMenu.DropDownItems.Add(new ToolStripSeparator());
        var importFolder = new ToolStripMenuItem("Import skin from folder…");
        importFolder.Click += (_, _) => Dispatcher.Invoke(SkinImporter.ImportFromFolder);
        modelMenu.DropDownItems.Add(importFolder);
        var importZip = new ToolStripMenuItem("Import skin from zip…");
        importZip.Click += (_, _) => Dispatcher.Invoke(SkinImporter.ImportFromZip);
        modelMenu.DropDownItems.Add(importZip);
        menu.Items.Add(modelMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restart DeskPet", null, (_, _) => Dispatcher.Invoke(Restart));
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(Shutdown));

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
    }

    private static Icon BuildIcon()
    {
        try
        {
            // Prefer a built-in resource if available; otherwise draw a simple paw.
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(Color.FromArgb(255, 250, 150, 40));
                g.FillEllipse(brush, 2, 2, 28, 28);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void OpenSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void Restart()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exe))
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        Shutdown();
    }

    private void PlayWelcomeSound()
    {
        try
        {
            AudioService.Instance.PlayWelcome();
        }
        catch
        {
            // ignore
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PetManager.Instance.Stop();
        MediaController.Instance.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
