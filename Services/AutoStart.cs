using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DeskPet.Services;

/// <summary>Manages "run at Windows startup" via the HKCU Run key.</summary>
public static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeskPet";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) != null;
            }
            catch { return false; }
        }
    }

    /// <summary>Registers or removes the startup entry. Uses the current exe path
    /// (handles the self-contained single-file exe, which counts as a real file).</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null) return;
            if (enabled)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
                // Quote the path so folder names with spaces work.
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch
        {
            // ignore — e.g. registry access denied
        }
    }
}
