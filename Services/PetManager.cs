using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskPet.Models;

namespace DeskPet.Services;

/// <summary>
/// Desktop pet state machine: animation, stats, behavior AI, movement,
/// home/out transitions, music linkage, and interactions.
/// Mirrors PetManager.swift.
/// </summary>
public sealed class PetManager : INotifyPropertyChanged
{
    public static PetManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private PetAction _action = PetAction.Idle;
    private bool _facingLeft;
    private BitmapSource? _currentFrame;
    private double _happiness = 100;
    private double _cleanliness = 100;
    private PetLifeState _lifeState = PetLifeState.Roaming;
    private bool _isDragging;
    private Point _windowPosition;
    private double _windowAlpha = 1;

    public PetAction Action { get => _action; private set => Set(ref _action, value); }
    public bool FacingLeft { get => _facingLeft; private set => Set(ref _facingLeft, value); }
    public BitmapSource? CurrentFrame { get => _currentFrame; private set => Set(ref _currentFrame, value); }
    public double Happiness { get => _happiness; private set => Set(ref _happiness, value); }
    public double Cleanliness { get => _cleanliness; private set => Set(ref _cleanliness, value); }
    public PetLifeState LifeState { get => _lifeState; private set => Set(ref _lifeState, value); }
    public bool IsDragging { get => _isDragging; private set => Set(ref _isDragging, value); }

    public Point WindowPosition { get => _windowPosition; private set => Set(ref _windowPosition, value); }
    public double WindowAlpha { get => _windowAlpha; private set => Set(ref _windowAlpha, value); }

    public PetSkin? Skin { get; private set; }

    public Size WindowSize
    {
        get
        {
            var baseSize = Skin?.BaseFrameSize ?? new Size(120, 120);
            double scale = AppSettings.Instance.PetScale * (Skin?.Scale ?? 1.0);
            return new Size(baseSize.Width * scale, baseSize.Height * scale);
        }
    }

    public bool IsHomeTransitioning => _movingHome;

    private DispatcherTimer? _animTimer;
    private DispatcherTimer? _statTimer;
    private int _tickCount;
    private double _frameProgress;
    private double _idleCounter = 4;
    private int _currentVariant;
    private int _lastFrameIndex = -1;
    private Point? _walkTarget;
    private bool _movingHome;
    private bool _fading;
    private bool _wasMusicPlaying;
    private bool _pendingSleep;
    private double _sleepHoldRemaining;
    private PetMovementMode _movementMode = PetMovementMode.Floor;
    private double _fallVelocity;
    private double? _fallTargetY; // where the out-of-home drop should land (null = bottom of screen)
    private const double WalkSpeed = 70;
    private const double Tick = 1.0 / 60.0;

    private PetManager()
    {
        Skin = PetSkin.Load();
    }

    public void Start()
    {
        if (_animTimer != null) return;
        // Use the property setter so the window is notified and positioned
        // immediately — assigning the backing field here left the window stuck
        // at (0,-110) and it only "teleported" into view on the first move.
        WindowPosition = InitialRoamingPosition();
        WindowAlpha = 1;
        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(Tick),
        };
        _animTimer.Tick += (_, _) => AnimTick();
        _animTimer.Start();

        _statTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _statTimer.Tick += (_, _) => StatTick();
        _statTimer.Start();

        _ = MediaController.Instance.StartAsync();
    }

    public void Stop()
    {
        _animTimer?.Stop();
        _animTimer = null;
        _statTimer?.Stop();
        _statTimer = null;
    }

    public void ReloadSkin()
    {
        Skin = PetSkin.Load();
        Action = PetAction.Idle;
        _frameProgress = 0;
        CurrentFrame = null;
        _currentVariant = 0;
        _lastFrameIndex = -1;
    }

    /// <summary>Re-reads the configured pet size and repositions the window.
    /// Called after the user changes "pet scale" so the size updates immediately.
    /// The skin stays cached — only the display scale changes.</summary>
    public void ApplyScale()
    {
        if (_lifeState == PetLifeState.Roaming)
        {
            _lastFrameIndex = -1;
            _frameProgress = 0;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WindowSize)));
    }

    /// <summary>Switch to a different built-in pet model and reload the skin.</summary>
    public void SwitchModel(string model)
    {
        AppSettings.Instance.PetModel = model;
        AppSettings.Instance.Save();
        ReloadSkin();
    }

    private void ResetIdleCounter()
    {
        double baseInterval = Math.Max(1.5, AppSettings.Instance.PetRoamInterval);
        _idleCounter = Random.Shared.NextDouble() * (baseInterval * 0.6) + baseInterval * 0.6;
    }

    // ------------------------------------------------------------------
    // Per-frame animation / AI
    // ------------------------------------------------------------------

    private void AnimTick()
    {
        _tickCount++;

        // 1. Music linkage
        if (AppSettings.Instance.PetMusicDance && (Skin?.HasRealMaterial(PetAction.Music) ?? false)
            && MediaController.Instance.IsPlaying && _movementMode == PetMovementMode.Floor)
        {
            if (!_wasMusicPlaying)
            {
                _wasMusicPlaying = true;
                _walkTarget = null;
                SetAction(PetAction.Music);
            }
        }
        else if (_wasMusicPlaying)
        {
            _wasMusicPlaying = false;
            if (!_isDragging && !_movingHome)
            {
                SetAction(PetAction.Idle);
                ResetIdleCounter();
            }
        }

        if (_movingHome && _walkTarget == null && !_fading)
        {
            _movingHome = false;
        }

        // 2. Movement
        switch (_movementMode)
        {
            case PetMovementMode.Floor:
                if (_walkTarget is { } target && !_isDragging && !_wasMusicPlaying
                    && (_lifeState == PetLifeState.Roaming || _movingHome))
                {
                    var position = _windowPosition;
                    double dx = target.X - position.X;
                    double dy = target.Y - position.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    double step = WalkSpeed * Tick;
                    if (distance <= step)
                    {
                        WindowPosition = target;
                        _walkTarget = null;
                        if (_movingHome)
                        {
                            // Arrived at the tray corner — fade out and go home.
                            _fading = true;
                            SetAction(PetAction.Idle);
                        }
                        else { SetAction(PetAction.Idle); ResetIdleCounter(); }
                    }
                    else
                    {
                        position.X += dx / distance * step;
                        position.Y += dy / distance * step;
                        WindowPosition = position;
                    }
                }
                break;

            case PetMovementMode.Falling:
                _fallVelocity += 500 * Tick;
                var pos = _windowPosition;
                pos.Y += _fallVelocity * Tick;
                // Floor line: never let the pet sink past the bottom of the work area.
                double floorY = WorkArea.Bottom - WindowSize.Height * 0.06;
                // If leaving home, land after a short drop instead of falling to the floor.
                double landY = _fallTargetY ?? floorY;
                landY = Math.Min(landY, floorY); // clamp to bottom (fallback)
                if (pos.Y >= landY)
                {
                    pos.Y = landY;
                    WindowPosition = pos;
                    _fallTargetY = null;
                    Land();
                }
                else
                {
                    WindowPosition = pos;
                }
                break;

            default:
                _movementMode = PetMovementMode.Floor;
                SetAction(PetAction.Idle);
                ResetIdleCounter();
                break;
        }

        // 3. Fade (home out / leave in)
        if (_fading)
        {
            double next = _windowAlpha + (_movingHome ? -0.06 : 0.06);
            if (_movingHome && next <= 0)
            {
                WindowAlpha = 0;
                _fading = false;
                LifeState = PetLifeState.AtHome;
                _movingHome = false;
                SetAction(PetAction.Home);
                Skin?.TrimToHomeOnly();
            }
            else if (!_movingHome && next >= 1)
            {
                WindowAlpha = 1;
                _fading = false;
                LifeState = PetLifeState.Roaming;
                SetAction(PetAction.Idle);
                ResetIdleCounter();
            }
            else
            {
                WindowAlpha = next;
            }
        }

        // 4. Frame advance
        var frames = Skin?.Frames(Action, _currentVariant);
        if (frames == null || frames.Count == 0)
        {
            if (Skin?.HasMaterial(Action) != true) CurrentFrame = null;
            return;
        }

        int count = frames.Count;
        int total = Skin!.TotalFrameCount(Action, _currentVariant);
        if (total == 0) total = count;
        double fps = Skin.Fps.TryGetValue(Action, out var f) ? f : Action.DefaultFps();
        bool isLoop = (Skin.Loops.TryGetValue(Action, out var loops) ? loops : (Action.LoopsByDefault() ? -1 : 1)) < 0;
        _frameProgress += Tick * fps;

        if (isLoop)
        {
            // Simple frame-0..count-1 loop. A "loop point" optimisation previously
            // jumped from the last frame to a middle frame, which looked like two
            // clips stitched together (a visible hitch at the seam) when the
            // signature match was imperfect.
            int idx = (int)_frameProgress % count;
            if (idx != _lastFrameIndex)
            {
                CurrentFrame = frames[idx];
                _lastFrameIndex = idx;
            }
        }
        else
        {
            int idx = Math.Min((int)_frameProgress, count - 1);
            if (idx != _lastFrameIndex)
            {
                CurrentFrame = frames[idx];
                _lastFrameIndex = idx;
            }
            if ((int)_frameProgress >= total - 1)
            {
                if (Action == PetAction.Yawn && _pendingSleep)
                {
                    _pendingSleep = false;
                    _frameProgress = 0;
                    SetAction(PetAction.Sleep);
                    _sleepHoldRemaining = Random.Shared.Next(5, 15);
                }
                else if (Action == PetAction.Sleep && _sleepHoldRemaining > 0)
                {
                    _sleepHoldRemaining -= Tick;
                    _frameProgress = count - 1;
                }
                else
                {
                    _frameProgress = 0;
                    if (!_isDragging)
                    {
                        SetAction(PetAction.Idle);
                        // If the sleep probability is dominant, re-enter sleep
                        // quickly instead of idling a long time — so "sleep = 100%"
                        // really looks like the pet mostly sleeping.
                        if (AppSettings.Instance.AiSleepChance >= 0.5
                            && AppSettings.Instance.AiSleepChance > AppSettings.Instance.AiWalkChance
                            && AppSettings.Instance.AiSleepChance > AppSettings.Instance.AiActionChance)
                        {
                            _idleCounter = Random.Shared.Next(2, 6);
                        }
                        else
                        {
                            ResetIdleCounter();
                        }
                    }
                }
            }
        }

        // 5. Behavior AI (only while idle on the floor)
        if (_lifeState == PetLifeState.Roaming && !_isDragging && !_wasMusicPlaying && !_movingHome
            && _movementMode == PetMovementMode.Floor && Action == PetAction.Idle)
        {
            _idleCounter -= Tick;
            if (_idleCounter <= 0) DecideNextBehavior();
        }
    }

    private void DecideNextBehavior()
    {
        bool sad = _happiness < 25;
        if (sad)
        {
            double r = Random.Shared.NextDouble();
            if (r < 0.30) StartSleeping();
            else if (r < 0.50) SetAction(PetAction.Hurt);
            else if (r < 0.70) StartWalking();
            else ResetIdleCounter();
            return;
        }

        // Weighted pick across walk / sleep / action / idle. Probabilities are
        // normalised against their sum, so idle is directly controllable and the
        // relative weights always behave intuitively (no branch can be unreachable).
        double walkC = Math.Max(0, AppSettings.Instance.AiWalkChance);
        double sleepC = Math.Max(0, AppSettings.Instance.AiSleepChance);
        double actC = Math.Max(0, AppSettings.Instance.AiActionChance);
        double idleC = Math.Max(0, AppSettings.Instance.AiIdleChance);
        double total = walkC + sleepC + actC + idleC;
        if (total <= 0) total = 1;
        double roll = Random.Shared.NextDouble() * total;

        if (roll < walkC)
        {
            StartWalking();
            return;
        }
        roll -= walkC;
        if (roll < sleepC)
        {
            StartSleeping();
            return;
        }
        roll -= sleepC;
        if (roll < actC)
        {
            double mini = Random.Shared.NextDouble();
            if (mini < 0.50) SetAction(PetAction.Yawn);
            else if (mini < 0.80) SetAction(PetAction.Happy);
            else SetAction(PetAction.Hurt);
            ResetIdleCounter();
            return;
        }

        // Idle: keep idling (optionally reshuffle the idle variant).
        ResetIdleCounter();
        if (Skin?.VariantCount(PetAction.Idle) > 1 && (Skin?.AllVariantsCached(PetAction.Idle) == true))
            SetAction(PetAction.Idle, true);
    }

    private void StartWalking()
    {
        var target = RandomWalkTarget();
        if (target == null) { ResetIdleCounter(); return; }
        _walkTarget = target.Value;
        UpdateFacing(target.Value);
        SetAction(PetAction.Walk);
    }

    private void StartSleeping()
    {
        _walkTarget = null;
        _pendingSleep = true;
        SetAction(PetAction.Yawn);
        _idleCounter = Random.Shared.Next(6, 14);
    }

    private Point? RandomWalkTarget()
    {
        var wa = WorkArea;
        var size = WindowSize;
        double minX = wa.Left + wa.Width * 0.06;
        double maxX = wa.Right - wa.Width * 0.06 - size.Width;
        double minY = wa.Top + wa.Height * 0.10;
        double maxY = wa.Top + wa.Height * 0.60;
        if (maxX <= minX || maxY <= minY) return null;

        var current = _windowPosition;
        double minDistance = wa.Width * 0.20;
        Point best = new(maxX, maxY);
        double bestDistance = 0;
        for (int i = 0; i < 10; i++)
        {
            var candidate = new Point(Random.Shared.NextDouble() * (maxX - minX) + minX,
                                      Random.Shared.NextDouble() * (maxY - minY) + minY);
            double dx = candidate.X - current.X, dy = candidate.Y - current.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > bestDistance) { best = candidate; bestDistance = d; }
            if (d >= minDistance) return candidate;
        }
        return best;
    }

    private void UpdateFacing(Point target)
    {
        FacingLeft = target.X < _windowPosition.X;
    }

    private void Land()
    {
        if (Math.Abs(_fallVelocity) > 300)
        {
            _fallVelocity = -_fallVelocity * 0.25;
        }
        else
        {
            _fallVelocity = 0;
            _movementMode = PetMovementMode.Floor;
            LifeState = PetLifeState.Roaming;
        }
    }

    // ------------------------------------------------------------------
    // Home / out
    //
    // "Home" for the pet is the floating window (which replaced the island).
    // The pet walks over to the floating window's current position, then fades.
    // ------------------------------------------------------------------

    /// <summary>Screen point the pet walks to when going home. Updated by the
    /// floating window as it moves; defaults to the tray corner.</summary>
    public static Point HomeAnchor { get; set; } = new Point(-1, -1);

    /// <summary>Screen-space hotspot around the tray overflow corner (bottom-right).</summary>
    public static Point TrayHomePoint
    {
        get
        {
            var wa = SystemParameters.WorkArea;
            return new Point(wa.Right, wa.Bottom);
        }
    }

    private static Point ResolveHomePoint()
    {
        var p = HomeAnchor;
        if (p.X < 0 || p.Y < 0) return TrayHomePoint;
        return p;
    }

    public void GoHome()
    {
        if (_lifeState != PetLifeState.Roaming || _isDragging || _movingHome) return;
        var size = WindowSize;
        var p = ResolveHomePoint();
        var target = new Point(p.X - size.Width / 2, p.Y - size.Height / 2);
        _walkTarget = null;
        _movingHome = true;
        _fading = false;
        _movementMode = PetMovementMode.Floor;
        UpdateFacing(target);
        _walkTarget = target;
        SetAction(PetAction.Walk);
    }

    public void LeaveHome()
    {
        if (_lifeState != PetLifeState.AtHome || _movingHome) return;
        var size = WindowSize;
        var p = ResolveHomePoint();
        _movementMode = PetMovementMode.Floor;
        // Emerge at the floating window, then drop by 25% of the screen width
        // (clamped to the work-area floor as a fallback).
        double startY = p.Y - size.Height / 2;
        WindowPosition = new Point(p.X - size.Width / 2, startY);
        _fallTargetY = startY + WorkArea.Width * 0.25;
        WindowAlpha = 1;
        _movingHome = false;
        _fading = false;
        LifeState = PetLifeState.Roaming;
        _movementMode = PetMovementMode.Falling;
        _fallVelocity = 0;
        SetAction(PetAction.Fall);
    }

    // ------------------------------------------------------------------
    // Interactions
    // ------------------------------------------------------------------

    public void Poke()
    {
        if (_lifeState != PetLifeState.Roaming || _isDragging || _movementMode != PetMovementMode.Floor) return;
        if (_happiness < 30)
        {
            // 心情差时戳一戳会委屈，心情略微下降
            SetAction(PetAction.Hurt);
            Happiness = Math.Max(0, _happiness - 1);
        }
        else
        {
            // 心情好时戳一戳 = 开心地蹦一下（happy 变体 0），轻微消耗心情
            SetAction(PetAction.Happy, forcedVariant: 0);
            Happiness = Math.Max(0, _happiness - 1);
        }
    }

    public void Pet()
    {
        if (_lifeState != PetLifeState.Roaming || _isDragging) return;
        // 抚摸 = 温柔地享受（happy 变体 1），显著恢复心情
        Happiness = Math.Min(100, _happiness + 12);
        SetAction(PetAction.Happy, forcedVariant: 1);
    }

    public void StartDrag()
    {
        if (_lifeState != PetLifeState.Roaming) return;
        IsDragging = true;
        _walkTarget = null;
        _movementMode = PetMovementMode.Floor;
        _movingHome = false;
        _fading = false;
        SetAction(PetAction.Drag);
    }

    public void EndDrag()
    {
        IsDragging = false;
        SetAction(PetAction.Idle);
        ResetIdleCounter();
    }

    public void SyncWindowPosition(Point point) => WindowPosition = point;

    // ------------------------------------------------------------------
    // Stats / action switching
    // ------------------------------------------------------------------

    private void StatTick()
    {
        double speed = AppSettings.Instance.PetStatDecaySpeed;
        double decay = 0.8 * speed;
        if (_lifeState == PetLifeState.AtHome)
        {
            // 回巢后清洁：以下降速度的 5 倍上升，逐步恢复到 100
            Cleanliness = Math.Min(100, _cleanliness + decay * 5);
        }
        else
        {
            Cleanliness = Math.Max(0, _cleanliness - decay);
        }
        Happiness = Math.Min(100, Math.Max(0, _happiness + (50 - _happiness) * 0.04));
    }

    private void SetAction(PetAction newAction, bool forceReshuffle = false, int? forcedVariant = null)
    {
        // Allow an explicit variant to re-trigger the same action (e.g. poke uses
        // happy variant 0 while pet uses variant 1), otherwise identical repeats are no-ops.
        if (!forceReshuffle && Action == newAction && (forcedVariant == null || forcedVariant == _currentVariant)) return;
        if (newAction != PetAction.Yawn && newAction != PetAction.Sleep)
        {
            _pendingSleep = false;
            _sleepHoldRemaining = 0;
        }
        Action = newAction;
        _frameProgress = 0;
        _lastFrameIndex = -1;
        Skin?.MarkPlayed(newAction);
        _currentVariant = forcedVariant ?? Skin?.PreferredVariant(newAction) ?? 0;
        var vc = Skin?.VariantCount(newAction) ?? 0;
        if (vc > 0 && _currentVariant >= vc) _currentVariant = vc - 1;

        if (forceReshuffle || newAction == PetAction.Idle || newAction == PetAction.Home)
        {
            AudioService.Instance.Stop();
        }
        else
        {
            var sound = Skin?.Sounds.GetValueOrDefault(newAction);
            AudioService.Instance.Play(sound, AppSettings.Instance.PetSoundVolume);
        }

        if (newAction == PetAction.Idle && _lifeState == PetLifeState.Roaming)
        {
            Skin?.Prefetch(new[] { PetAction.Idle, PetAction.Walk, PetAction.Yawn, PetAction.Drag, PetAction.Happy, PetAction.Hurt, PetAction.Fall });
        }
    }

    public void UpdateSoundVolume() => AudioService.Instance.SetVolume(AppSettings.Instance.PetSoundVolume);

    // ------------------------------------------------------------------
    // Screen helpers
    // ------------------------------------------------------------------

    private static Rect WorkArea => SystemParameters.WorkArea;

    private Point InitialRoamingPosition()
    {
        var wa = WorkArea;
        var size = WindowSize;
        return new Point(wa.Left + wa.Width / 2 - size.Width / 2, wa.Top + wa.Height * 0.22);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
