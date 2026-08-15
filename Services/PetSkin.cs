using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskPet.Models;

namespace DeskPet.Services;

public sealed class PetSkinConfig
{
    public double? fps { get; set; }
    public double? scale { get; set; }
    public Dictionary<string, PetActionConfig>? actions { get; set; }
}

public sealed class PetActionConfig
{
    public double? fps { get; set; }
    public int? loops { get; set; }
}

/// <summary>
/// Loads a pet "skin" (sequence-frame PNG animation) from a folder following
/// the PET.md convention: <action>[_N]/1.png, 2.png, ...
/// Mirrors SpriteAnimator.swift / PetSkin in the macOS app.
/// </summary>
public sealed class PetSkin
{
    private static readonly PetAction[] AllActions = Enum.GetValues<PetAction>();

    private readonly Dictionary<PetAction, List<List<string>>> _variantFiles = new();
    private readonly Dictionary<PetAction, List<List<BitmapSource>>> _cache = new();
    private readonly Dictionary<PetAction, List<int>> _loopPoints = new();
    private readonly HashSet<string> _loading = new();
    private readonly HashSet<string> _partialLoaded = new();

    public Dictionary<PetAction, double> Fps { get; } = new();
    public Dictionary<PetAction, int> Loops { get; } = new();
    public Dictionary<PetAction, string> Sounds { get; } = new();
    public double Scale { get; }
    public Size? BaseFrameSize { get; }
    public string SourceDirectory { get; }
    public int MaxPixelSize { get; }

    private const int TrailerFrameCount = 24;
    private static readonly HashSet<PetAction> CoreActions = new()
    {
        PetAction.Idle, PetAction.Home, PetAction.Drag, PetAction.Happy, PetAction.Hurt, PetAction.Fall,
    };

    private PetSkin(string dir, Dictionary<PetAction, List<List<string>>> files,
                    double scale, Size? baseSize, int maxPixel)
    {
        SourceDirectory = dir;
        _variantFiles = files;
        Scale = scale;
        BaseFrameSize = baseSize;
        MaxPixelSize = maxPixel;
    }

    public int VariantCount(PetAction action) =>
        _variantFiles.TryGetValue(action, out var v) ? v.Count : 0;

    public int TotalFrameCount(PetAction action, int variant)
    {
        if (_variantFiles.TryGetValue(action, out var v) && variant >= 0 && variant < v.Count)
            return v[variant].Count;
        return 0;
    }

    public bool HasRealMaterial(PetAction action) =>
        _variantFiles.TryGetValue(action, out var v) && v.Count > 0 && v[0].Count > 0;

    public bool HasMaterial(PetAction action)
    {
        if (HasRealMaterial(action)) return true;
        return action switch
        {
            PetAction.Music or PetAction.Hang or PetAction.Dizzy or PetAction.Yawn => _variantFiles.ContainsKey(PetAction.Idle),
            PetAction.Climb => _variantFiles.ContainsKey(PetAction.Walk) || _variantFiles.ContainsKey(PetAction.Idle),
            PetAction.Fall => _variantFiles.ContainsKey(PetAction.Drag) || _variantFiles.ContainsKey(PetAction.Idle),
            PetAction.Home => _variantFiles.ContainsKey(PetAction.Sleep) || _variantFiles.ContainsKey(PetAction.Idle),
            _ => false,
        };
    }

    public int LoopPoint(PetAction action, int variant) =>
        _loopPoints.TryGetValue(action, out var pts) && variant >= 0 && variant < pts.Count ? pts[variant] : 0;

    /// <summary>Returns cached frames for an action/variant, or null if still loading.</summary>
    public List<BitmapSource>? Frames(PetAction action, int variant = -1)
    {
        var v = ResolveVariant(action, variant);
        if (_cache.TryGetValue(action, out var variants) && v < variants.Count && variants[v].Count > 0)
            return variants[v];

        if (_variantFiles.TryGetValue(action, out var urls) && v < urls.Count && urls[v].Count > 0)
        {
            var key = $"{action}_{v}";
            if (_loading.Add(key))
            {
                StartLoad(action, v, urls[v]);
            }
            // During load, fall back to another cached variant of the same action.
            if (_cache.TryGetValue(action, out var existing))
            {
                foreach (var e in existing) if (e.Count > 0) return e;
            }
            return null;
        }

        return FallbackFrames(action);
    }

    public void Prefetch(IEnumerable<PetAction> actions)
    {
        foreach (var action in actions)
        {
            if (!_variantFiles.TryGetValue(action, out var variants) || variants.Count == 0) continue;
            if (action == PetAction.Idle)
            {
                for (int vi = 0; vi < variants.Count; vi++)
                {
                    if (variants[vi].Count == 0) continue;
                    if (_cache.TryGetValue(action, out var c) && vi < c.Count && c[vi].Count > 0) continue;
                    var key = $"{action}_{vi}";
                    if (_loading.Add(key)) StartLoad(action, vi, variants[vi]);
                }
            }
            else
            {
                var first = variants.FirstOrDefault(u => u.Count > 0);
                if (first == null) continue;
                if (_cache.TryGetValue(action, out var c) && c.Count > 0 && c[0].Count > 0) continue;
                var key = $"{action}_0";
                if (_loading.Add(key)) StartLoad(action, 0, first);
            }
        }
    }

    public int? PreferredVariant(PetAction action)
    {
        if (!_variantFiles.TryGetValue(action, out var variants) || variants.Count == 0) return null;
        var cached = new List<int>();
        if (_cache.TryGetValue(action, out var c))
        {
            for (int i = 0; i < c.Count; i++) if (c[i].Count > 0) cached.Add(i);
        }
        if (cached.Count > 0) return cached[Random.Shared.Next(cached.Count)];
        return Random.Shared.Next(variants.Count);
    }

    public void MarkPlayed(PetAction action)
    {
        // Simplify vs. the Swift version: keep core + current, evict the rest.
        foreach (var key in _cache.Keys.ToList())
        {
            if (key != action && !CoreActions.Contains(key)) _cache.Remove(key);
        }
    }

    public void TrimToHomeOnly()
    {
        foreach (var key in _cache.Keys.ToList())
        {
            if (key != PetAction.Home && key != PetAction.Fall) _cache.Remove(key);
        }
    }

    private void StartLoad(PetAction action, int variant, List<string> urls)
    {
        var key = $"{action}_{variant}";
        var maxPixel = MaxPixelSize;
        Task.Run(() =>
        {
            var images = new List<BitmapSource>();
            var sigs = new List<float[]>();
            foreach (var url in urls)
            {
                var img = DecodeDownscaled(url, maxPixel);
                if (img != null) images.Add(img);
                sigs.Add(FrameSignature(url));
            }
            var loop = ComputeLoopPoint(sigs);
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _loading.Remove(key);
                if (images.Count == 0) return;
                Store(action, variant, images, loop);
                if (images.Count < urls.Count) _partialLoaded.Add(key);
            });
        });
    }

    private void Store(PetAction action, int variant, List<BitmapSource> images, int loop)
    {
        if (!_cache.TryGetValue(action, out var variants))
        {
            variants = new List<List<BitmapSource>>();
            _cache[action] = variants;
        }
        while (variants.Count <= variant) variants.Add(new List<BitmapSource>());
        variants[variant] = images;

        if (!_loopPoints.TryGetValue(action, out var pts))
        {
            pts = new List<int>();
            _loopPoints[action] = pts;
        }
        while (pts.Count <= variant) pts.Add(0);
        pts[variant] = loop;
    }

    private int ResolveVariant(PetAction action, int requested)
    {
        if (!_variantFiles.TryGetValue(action, out var variants) || variants.Count == 0) return 0;
        if (requested >= 0 && requested < variants.Count) return requested;
        return Random.Shared.Next(variants.Count);
    }

    private List<BitmapSource>? FallbackFrames(PetAction action)
    {
        if (action == PetAction.Home)
        {
            var sleep = Frames(PetAction.Sleep);
            if (sleep != null && sleep.Count > 0) return new List<BitmapSource> { sleep[^1] };
            var idle = Frames(PetAction.Idle);
            if (idle != null && idle.Count > 0) return new List<BitmapSource> { idle[0] };
            return null;
        }
        return action switch
        {
            PetAction.Music => Frames(PetAction.Idle),
            PetAction.Climb => Frames(PetAction.Walk) ?? Frames(PetAction.Idle),
            PetAction.Hang or PetAction.Dizzy or PetAction.Yawn => Frames(PetAction.Idle),
            PetAction.Fall => Frames(PetAction.Drag) ?? Frames(PetAction.Idle),
            _ => null,
        };
    }

    // ---------------------------------------------------------------------
    // Decoding / signatures
    // ---------------------------------------------------------------------

    public static BitmapSource? DecodeDownscaled(string path, int maxPixel)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            int w = frame.PixelWidth, h = frame.PixelHeight;
            if (w <= 0 || h <= 0) return null;
            double scale = Math.Max(w, h) > maxPixel ? (double)maxPixel / Math.Max(w, h) : 1.0;
            if (scale >= 1.0)
            {
                var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                return converted;
            }
            if (frame.CanFreeze) frame.Freeze();
            var transformed = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            var result = new FormatConvertedBitmap(transformed, PixelFormats.Bgra32, null, 0);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>16x16 grid signature (luminance + alpha) computed from a 32px thumbnail.</summary>
    public static float[] FrameSignature(string path)
    {
        const int grid = 16;
        var sig = new float[grid * grid * 2];
        try
        {
            var thumb = DecodeDownscaled(path, 32);
            if (thumb == null) return sig;
            int w = thumb.PixelWidth, h = thumb.PixelHeight;
            var pixels = new byte[w * h * 4];
            thumb.CopyPixels(pixels, w * 4, 0);
            for (int gy = 0; gy < grid; gy++)
            {
                for (int gx = 0; gx < grid; gx++)
                {
                    float lum = 0, alpha = 0, cnt = 0;
                    int cw = Math.Max(1, w / grid), ch = Math.Max(1, h / grid);
                    for (int sy = 0; sy < ch; sy++)
                    {
                        for (int sx = 0; sx < cw; sx++)
                        {
                            int x = Math.Min(w - 1, gx * (w / grid) + sx);
                            int y = Math.Min(h - 1, gy * (h / grid) + sy);
                            int i = (y * w + x) * 4;
                            float a = pixels[i + 3] / 255f;
                            if (a > 0.05f)
                            {
                                lum += (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3f / 255f * a;
                                alpha += a;
                                cnt += 1;
                            }
                        }
                    }
                    float n = Math.Max(1, cw) * Math.Max(1, ch);
                    sig[gy * grid + gx] = cnt > 0 ? lum / n : 0;
                    sig[grid * grid + gy * grid + gx] = alpha / n;
                }
            }
        }
        catch { }
        return sig;
    }

    public static int ComputeLoopPoint(List<float[]> signatures)
    {
        if (signatures.Count <= 3) return 0;
        int last = signatures.Count - 1;
        int limit = Math.Max(1, (int)(signatures.Count * 0.7));
        int best = 0;
        float bestDist = float.MaxValue;
        for (int s = 0; s < limit; s++)
        {
            float d = SignatureDistance(signatures[s], signatures[last]);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        float defaultDist = SignatureDistance(signatures[0], signatures[last]);
        return bestDist < defaultDist * 0.5 ? best : 0;
    }

    private static float SignatureDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length) return float.MaxValue;
        float d = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            d += diff * diff;
        }
        return d;
    }

    // ---------------------------------------------------------------------
    // Skin location / loading
    // ---------------------------------------------------------------------

    public static string DefaultSkinDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "BoringPet");

    public static string BuiltinModelsRoot =>
        Path.Combine(DefaultSkinDirectory, BuiltinSkinGenerator.BuiltinDirName);

    public static string ImportedModelsRoot =>
        Path.Combine(DefaultSkinDirectory, "imported");

    /// <summary>Lists available pet model names (built-in + imported).</summary>
    public static List<string> GetAvailableModels()
    {
        var result = new List<string>();
        foreach (var root in new[] { BuiltinModelsRoot, ImportedModelsRoot })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var d in Directory.GetDirectories(root))
            {
                if (HasAnyFrames(d)) result.Add(Path.GetFileName(d));
            }
        }
        return result.Distinct().ToList();
    }

    /// <summary>Resolves a model name to its directory path.</summary>
    public static string? ResolveModelPath(string name)
    {
        foreach (var root in new[] { BuiltinModelsRoot, ImportedModelsRoot })
        {
            var p = Path.Combine(root, name);
            if (Directory.Exists(p) && HasAnyFrames(p)) return p;
        }
        return null;
    }

    private static bool HasAnyFrames(string dir)
    {
        if (!Directory.Exists(dir)) return false;
        return Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Any(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                      || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
    }

    public static string? LocateSkinDirectory()
    {
        var custom = AppSettings.Instance.PetSkinDirectory;
        if (!string.IsNullOrWhiteSpace(custom) && Directory.Exists(custom)) return custom;

        var models = GetAvailableModels();
        if (models.Count > 0)
        {
            var selected = AppSettings.Instance.PetModel;
            if (!string.IsNullOrEmpty(selected) && models.Contains(selected))
            {
                var p = ResolveModelPath(selected);
                if (p != null) return p;
            }
            return ResolveModelPath(models[0]) ?? Path.Combine(BuiltinModelsRoot, models[0]);
        }

        // Legacy: a custom skin placed directly at the default directory root.
        if (HasAnyFrames(DefaultSkinDirectory)) return DefaultSkinDirectory;

        return null;
    }

    // ---------------------------------------------------------------------
    // Skin import (folder or zip → a new model under imported/<name>)
    // ---------------------------------------------------------------------

    /// <summary>Imports a skin folder as a new pet model.</summary>
    public static string ImportSkinFolder(string sourceDir, string name)
    {
        var target = Path.Combine(ImportedModelsRoot, name);
        if (Directory.Exists(target)) Directory.Delete(target, true);
        Directory.CreateDirectory(target);
        CopyDirectory(sourceDir, target);
        return target;
    }

    /// <summary>Imports a skin zip archive as a new pet model.</summary>
    public static string ImportSkinZip(string zipPath, string name)
    {
        var target = Path.Combine(ImportedModelsRoot, name);
        if (Directory.Exists(target)) Directory.Delete(target, true);
        Directory.CreateDirectory(target);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, target);
        NormalizeSkinRoot(target);
        return target;
    }

    /// <summary>If a zip unpacked to one wrapper folder, move its contents up.</summary>
    private static void NormalizeSkinRoot(string dir)
    {
        if (ContainsActionFolder(dir)) return;
        var sub = Directory.GetDirectories(dir);
        if (sub.Length == 1 && ContainsActionFolder(sub[0]))
        {
            var tmp = dir + "_tmp";
            Directory.Move(sub[0], tmp);
            Directory.Delete(dir, true);
            Directory.Move(tmp, dir);
        }
    }

    private static bool ContainsActionFolder(string dir)
    {
        if (!Directory.Exists(dir)) return false;
        var actionNames = new HashSet<string>(Enum.GetNames<PetAction>().Select(n => n.ToLowerInvariant()));
        foreach (var d in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(d).ToLowerInvariant();
            int underscore = name.LastIndexOf('_');
            var baseName = underscore > 0 ? name[..underscore] : name;
            if (actionNames.Contains(baseName)) return true;
        }
        return false;
    }

    private static void CopyDirectory(string src, string dst)
    {
        foreach (var d in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(d.Replace(src, dst));
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(f, f.Replace(src, dst), true);
    }

    public static PetSkin? Load()
    {
        var dir = LocateSkinDirectory();
        if (dir == null) return null;

        var config = new PetSkinConfig();
        var configPath = Path.Combine(dir, "config.json");
        if (File.Exists(configPath))
        {
            try { config = JsonSerializer.Deserialize<PetSkinConfig>(File.ReadAllText(configPath)) ?? new(); } catch { }
        }

        var variantFiles = new Dictionary<PetAction, List<List<string>>>();
        var fps = new Dictionary<PetAction, double>();
        var loops = new Dictionary<PetAction, int>();
        var sounds = new Dictionary<PetAction, string>();

        var allFolders = Directory.Exists(dir)
            ? Directory.GetDirectories(dir).Where(d => !Path.GetFileName(d).StartsWith(".")).ToList()
            : new List<string>();

        // First pass: variant folders (idle_0, idle_1, ...)
        var variantFolders = new Dictionary<PetAction, List<(int idx, string url)>>();
        foreach (var folder in allFolders)
        {
            var name = Path.GetFileName(folder);
            int underscore = name.LastIndexOf('_');
            if (underscore > 0 && int.TryParse(name[(underscore + 1)..], out int idx))
            {
                string baseName = name[..underscore];
                var action = AllActions.FirstOrDefault(a => a.FolderName() == baseName);
                if (action.FolderName() == baseName)
                {
                    if (!variantFolders.ContainsKey(action)) variantFolders[action] = new();
                    variantFolders[action].Add((idx, folder));
                }
            }
        }

        foreach (var (action, variants) in variantFolders)
        {
            var sorted = variants.OrderBy(v => v.idx).ToList();
            var urlsList = new List<List<string>>();
            foreach (var (_, folder) in sorted)
            {
                var urls = ScanPngs(folder);
                if (urls.Count > 0) urlsList.Add(urls);
                if (!sounds.ContainsKey(action))
                {
                    var s = ScanSound(folder);
                    if (s != null) sounds[action] = s;
                }
            }
            if (urlsList.Count > 0)
            {
                variantFiles[action] = urlsList;
                ApplyConfig(action, config, fps, loops);
            }
        }

        // Second pass: single-action folders
        foreach (var action in AllActions)
        {
            if (variantFiles.ContainsKey(action)) continue;
            var folder = Path.Combine(dir, action.FolderName());
            if (!Directory.Exists(folder)) continue;
            var urls = ScanPngs(folder);
            if (urls.Count == 0) continue;
            variantFiles[action] = new List<List<string>> { urls };
            ApplyConfig(action, config, fps, loops);
            var s = ScanSound(folder);
            if (s != null) sounds[action] = s;
        }

        if (variantFiles.Count == 0) return null;

        // Read base frame size (original pixel dimensions) from idle first frame.
        double scale = config.scale ?? 1.0;
        Size? baseSize = null;
        int widest = 0;
        foreach (var (action, variants) in variantFiles)
        {
            var first = variants.FirstOrDefault()?.FirstOrDefault();
            if (first == null) continue;
            var dims = ReadPixelDimensions(first);
            if (dims != null)
            {
                widest = Math.Max(widest, dims.Value.w);
                if (action == PetAction.Idle) baseSize = new Size(dims.Value.w, dims.Value.h);
            }
        }

        double targetPixel = Math.Max(widest, 1) * AppSettings.Instance.PetScale * scale * 2.0;
        int maxPixelSize = Math.Min(300, Math.Max(256, (int)targetPixel));

        var skin = new PetSkin(dir, variantFiles, scale, baseSize, maxPixelSize);
        foreach (var kv in fps) skin.Fps[kv.Key] = kv.Value;
        foreach (var kv in loops) skin.Loops[kv.Key] = kv.Value;
        foreach (var kv in sounds) skin.Sounds[kv.Key] = kv.Value;

        // Synchronously load idle variant 0 trailer so the pet shows immediately.
        if (variantFiles.TryGetValue(PetAction.Idle, out var idleVariants) && idleVariants.Count > 0)
        {
            var firstVariant = idleVariants[0];
            var images = new List<BitmapSource>();
            var sigs = new List<float[]>();
            foreach (var url in firstVariant.Take(TrailerFrameCount))
            {
                var img = DecodeDownscaled(url, maxPixelSize);
                if (img != null) images.Add(img);
                sigs.Add(FrameSignature(url));
            }
            if (images.Count > 0)
            {
                skin.Store(PetAction.Idle, 0, images, ComputeLoopPoint(sigs));
                if (images.Count < firstVariant.Count) skin._partialLoaded.Add("Idle_0");
            }
        }

        skin.Prefetch(new[] { PetAction.Idle, PetAction.Walk, PetAction.Yawn, PetAction.Drag, PetAction.Happy, PetAction.Hurt, PetAction.Fall });
        return skin;
    }

    private static void ApplyConfig(PetAction action, PetSkinConfig config,
                                    Dictionary<PetAction, double> fps, Dictionary<PetAction, int> loops)
    {
        var actionConfig = config.actions?.GetValueOrDefault(action.FolderName());
        fps[action] = actionConfig?.fps ?? config.fps ?? action.DefaultFps();
        loops[action] = actionConfig?.loops ?? (action.LoopsByDefault() ? -1 : 1);
    }

    private static List<string> ScanPngs(string folder)
    {
        if (!Directory.Exists(folder)) return new List<string>();
        return Directory.GetFiles(folder)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
            })
            .OrderBy(f => Path.GetFileName(f), NaturalStringComparer.Instance)
            .ToList();
    }

    private static string? ScanSound(string folder)
    {
        foreach (var ext in new[] { ".m4a", ".mp3", ".wav", ".aiff" })
        {
            var url = Path.Combine(folder, "sound" + ext);
            if (File.Exists(url)) return url;
        }
        return null;
    }

    private static (int w, int h)? ReadPixelDimensions(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch { return null; }
    }

    /// <summary>Generate the built-in pet models so the app works out of the box.</summary>
    public static void EnsureDefaultSkin()
    {
        if (GetAvailableModels().Count == 0)
        {
            try
            {
                BuiltinSkinGenerator.GenerateAll();
            }
            catch
            {
                // ignore — placeholder rendering will be used instead
            }
        }
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new();
        public int Compare(string? x, string? y)
        {
            if (x == null || y == null) return string.CompareOrdinal(x, y);
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
