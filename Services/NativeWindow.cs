using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeskPet.Services;

/// <summary>
/// Small Win32 helpers for tweaking WPF window styles that XAML can't express
/// directly (e.g. hiding a window from Alt+Tab).
/// </summary>
public static class NativeWindow
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;   // hide from Alt+Tab / taskbar
    private const int WsExAppWindow = 0x00040000;    // "app window" bit that forces a taskbar entry
    private const int WsExTransparent = 0x00000020;  // click-through: pass mouse to window below

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>Mark the window as a tool window so it no longer appears in
    /// Alt+Tab or the taskbar. Call once the HWND exists (SourceInitialized).</summary>
    public static void HideFromAltTab(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        long ex = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        ex |= WsExToolWindow;       // tool window: hidden from Alt+Tab
        ex &= ~WsExAppWindow;       // clear the app-window bit so it can't force a taskbar entry
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(ex));
    }

    /// <summary>Toggle click-through on a window. When enabled, mouse events pass
    /// through this window and operate on the window below it (HTTRANSPARENT).</summary>
    public static void SetClickThrough(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        long ex = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        if (enabled) ex |= WsExTransparent;
        else ex &= ~WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(ex));
    }
}
