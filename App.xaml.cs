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

        // Global exception logging — prevents silent crashes and captures the
        // stack for diagnosing the intermittent interaction freeze/crash.
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnTaskUnobserved;

        // Ensure the default skin folder exists with a built-in placeholder skin
        PetSkin.EnsureDefaultSkin();

        // Sync any skin packs bundled in the app's Skins/ into the user's skin folder
        PetSkin.SyncBundledSkins();

        AppSettings.Instance.Load();

        CreateTrayIcon();

        // Circular floating window (replaces the island) with the action menu.
        FloatWindow.Instance.ShowFloat();

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
            Text = "DeskPet 桌面宠物",
            Visible = true,
            Icon = BuildIcon(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("和宠物对话", null, (_, _) => Dispatcher.Invoke(ChatWindow.ShowChat));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("放宠物出来", null, (_, _) => Dispatcher.Invoke(() => PetManager.Instance.LeaveHome()));
        menu.Items.Add("让宠物回家", null, (_, _) => Dispatcher.Invoke(() => PetManager.Instance.GoHome()));

        // Pet model submenu
        var modelMenu = new ToolStripMenuItem("宠物形象");
        foreach (var model in PetSkin.GetAvailableModels())
        {
            var item = new ToolStripMenuItem(model) { Checked = model == AppSettings.Instance.PetModel };
            item.Click += (_, _) => Dispatcher.Invoke(() => PetManager.Instance.SwitchModel(model));
            modelMenu.DropDownItems.Add(item);
        }
        modelMenu.DropDownItems.Add(new ToolStripSeparator());
        var importFolder = new ToolStripMenuItem("从文件夹导入皮肤…");
        importFolder.Click += (_, _) => Dispatcher.Invoke(SkinImporter.ImportFromFolder);
        modelMenu.DropDownItems.Add(importFolder);
        var importZip = new ToolStripMenuItem("从压缩包导入皮肤…");
        importZip.Click += (_, _) => Dispatcher.Invoke(SkinImporter.ImportFromZip);
        modelMenu.DropDownItems.Add(importZip);
        menu.Items.Add(modelMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("重启 DeskPet", null, (_, _) => Dispatcher.Invoke(Restart));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Shutdown));

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

    public void Restart()
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

    // ---- Global exception logging ----

    private static string CrashLogPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeskPet", "crash.log");

    private static void LogCrash(string source, Exception ex)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(CrashLogPath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\r\n{ex}\r\n\r\n");
        }
        catch { /* ignore logging failures */ }
    }

    private void OnDispatcherUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("DispatcherUnhandled", e.Exception);
        e.Handled = true; // keep the app alive rather than crashing outright
    }

    private void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) LogCrash("AppDomainUnhandled", ex);
    }

    private void OnTaskUnobserved(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("UnobservedTask", e.Exception);
        e.SetObserved();
    }
}
