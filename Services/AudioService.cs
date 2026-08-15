using System;
using System.Windows.Media;

namespace DeskPet.Services;

/// <summary>Plays pet action sounds and the welcome sound.</summary>
public sealed class AudioService
{
    public static AudioService Instance { get; } = new();

    private MediaPlayer? _player;

    public void Play(string? path, double volume)
    {
        Stop();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var player = new MediaPlayer();
            player.Open(new Uri(path, UriKind.Absolute));
            player.Volume = Math.Clamp(volume, 0, 1);
            player.Play();
            _player = player;
        }
        catch
        {
            // ignore unsupported audio formats
        }
    }

    public void SetVolume(double volume)
    {
        if (_player != null) _player.Volume = Math.Clamp(volume, 0, 1);
    }

    public void Stop()
    {
        try { _player?.Close(); } catch { }
        _player = null;
    }

    public void PlayWelcome()
    {
        try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
    }
}
