using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace DeskPet.Services;

/// <summary>
/// Reads and controls the currently-playing media session on Windows via the
/// System Media Transport Controls (SMTC). This is the Windows equivalent of
/// the macOS NowPlaying controller and works with Spotify, the Groove/Media
/// apps, web players (Chrome/Edge), etc.
/// </summary>
public sealed class MediaController : INotifyPropertyChanged, IDisposable
{
    public static MediaController Instance { get; } = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _started;
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _title = "DeskPet";
    private string _artist = "No media playing";
    private string _album = "";
    private BitmapImage? _albumArt;
    private bool _isPlaying;
    private bool _isIdle = true;
    private double _position;
    private double _duration;
    private bool _isShuffled;
    private int _repeatMode; // 0=off, 1=all, 2=one

    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Artist { get => _artist; private set => Set(ref _artist, value); }
    public string Album { get => _album; private set => Set(ref _album, value); }
    public BitmapImage? AlbumArt { get => _albumArt; private set => Set(ref _albumArt, value); }
    public bool IsPlaying { get => _isPlaying; private set { if (Set(ref _isPlaying, value)) IsIdle = !value; } }
    public bool IsIdle { get => _isIdle; private set => Set(ref _isIdle, value); }
    public double Position { get => _position; private set => Set(ref _position, value); }
    public double Duration { get => _duration; private set => Set(ref _duration, value); }
    public bool IsShuffled { get => _isShuffled; private set => Set(ref _isShuffled, value); }
    public int RepeatMode { get => _repeatMode; private set => Set(ref _repeatMode, value); }

    private MediaController() { }

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachSession(_manager.GetCurrentSession());
            await RefreshAsync();
        }
        catch
        {
            // SMTC may be unavailable on some systems
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        AttachSession(sender.GetCurrentSession());
        _ = RefreshAsync();
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        _session = session;
        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelineChanged;
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();
    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync();
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => _ = RefreshTimelineAsync();

    public async Task RefreshAsync()
    {
        if (_session == null) { Reset(); return; }
        try
        {
            var props = _session.TryGetMediaPropertiesAsync();
            var info = _session.GetPlaybackInfo();
            var media = await props;
            Title = media.Title ?? "Unknown";
            Artist = media.Artist ?? "Unknown";
            Album = media.AlbumTitle ?? "";

            if (media.Thumbnail != null)
            {
                using var stream = await media.Thumbnail.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(ms);
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = new MemoryStream(ms.ToArray());
                img.EndInit();
                img.Freeze();
                AlbumArt = img;
            }

            IsPlaying = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            IsShuffled = info?.IsShuffleActive == true;
            RepeatMode = MapRepeat(info?.AutoRepeatMode);
            await RefreshTimelineAsync();
        }
        catch
        {
            Reset();
        }
    }

    private async Task RefreshTimelineAsync()
    {
        if (_session == null) return;
        try
        {
            var timeline = _session.GetTimelineProperties();
            if (timeline != null)
            {
                Duration = timeline.EndTime.TotalSeconds;
                Position = timeline.Position.TotalSeconds;
            }
        }
        catch { }
        await Task.CompletedTask;
    }

    private static int MapRepeat(Windows.Media.MediaPlaybackAutoRepeatMode? mode) => mode switch
    {
        Windows.Media.MediaPlaybackAutoRepeatMode.List => 1,
        Windows.Media.MediaPlaybackAutoRepeatMode.Track => 2,
        _ => 0,
    };

    private void Reset()
    {
        Title = "DeskPet";
        Artist = "No media playing";
        Album = "";
        IsPlaying = false;
        IsIdle = true;
        Position = 0;
        Duration = 0;
    }

    // ---- Control methods ----

    public async Task PlayPauseAsync() => await Safe(() => _session!.TryTogglePlayPauseAsync());
    public async Task PlayAsync() => await Safe(() => _session!.TryPlayAsync());
    public async Task PauseAsync() => await Safe(() => _session!.TryPauseAsync());
    public async Task NextAsync() => await Safe(() => _session!.TrySkipNextAsync());
    public async Task PreviousAsync() => await Safe(() => _session!.TrySkipPreviousAsync());
    public async Task ToggleShuffleAsync() => await Safe(() => _session!.TryChangeShuffleActiveAsync(!IsShuffled));
    public async Task ToggleRepeatAsync() => await Safe(() => _session!.TryChangeAutoRepeatModeAsync(RepeatMode == 2 ? Windows.Media.MediaPlaybackAutoRepeatMode.None : Windows.Media.MediaPlaybackAutoRepeatMode.List));

    public async Task SeekAsync(double seconds)
    {
        if (_session == null) return;
        try
        {
            await _session.TryChangePlaybackPositionAsync((long)(seconds * 10_000_000));
            Position = seconds;
        }
        catch { }
    }

    private static async Task Safe(Func<Windows.Foundation.IAsyncOperation<bool>> op)
    {
        try { await op(); } catch { }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_manager != null) _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
    }
}
