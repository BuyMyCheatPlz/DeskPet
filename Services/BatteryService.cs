using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Threading;

namespace DeskPet.Services;

/// <summary>Reads battery status (level %, charging, plugged).</summary>
public sealed class BatteryService : INotifyPropertyChanged
{
    public static BatteryService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private int _level = 100;
    private bool _charging;
    private bool _plugged;

    public int Level { get => _level; private set => Set(ref _level, value); }
    public bool Charging { get => _charging; private set => Set(ref _charging, value); }
    public bool Plugged { get => _plugged; private set => Set(ref _plugged, value); }

    internal BatteryService()
    {
        Refresh();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    private void Refresh()
    {
        var ps = SystemInformation.PowerStatus;
        double pct = ps.BatteryLifePercent * 100;
        if (double.IsNaN(pct) || pct < 0) pct = 100;
        Level = (int)Math.Round(pct);
        Charging = ps.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging);
        Plugged = ps.PowerLineStatus == PowerLineStatus.Online;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
