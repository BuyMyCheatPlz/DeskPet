using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DeskPet.Models;

/// <summary>
/// Persistent settings, mirroring the macOS "Defaults" keys. Stored as JSON
/// under %APPDATA%/DeskPet/settings.json.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    public static AppSettings Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string StorageDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskPet");

    private static string StoragePath => Path.Combine(StorageDir, "settings.json");

    // ---- General / Behavior ----
    public bool EnableHaptics { get; set; } = true;

    // ---- Desktop Pet ----
    public bool PetEnabled { get; set; } = true;
    /// <summary>Selected built-in pet model name (cat/dog/rabbit/panda).</summary>
    public string PetModel { get; set; } = "cat";
    public string PetSkinDirectory { get; set; } = "";
    public double PetScale { get; set; } = 0.75;
    public double PetStatDecaySpeed { get; set; } = 0.5;
    public bool PetMusicDance { get; set; } = true;
    public double PetRoamInterval { get; set; } = 30.0;
    public double PetSoundVolume { get; set; } = 0.25;

    // ---- AI chat ----
    /// <summary>"deepseek" | "openai" | "custom"</summary>
    public string AiProvider { get; set; } = "deepseek";
    public string AiApiKey { get; set; } = "";
    public string AiModel { get; set; } = "deepseek-chat";
    public string AiBaseUrl { get; set; } = "https://api.deepseek.com";

    // ---- Session flags (not shown in settings) ----
    public bool FirstLaunch { get; set; } = true;

    // ---- Notch geometry (constants, matching the macOS sizing) ----
    public static double ClosedNotchWidth => 185;
    public static double ClosedNotchHeight => 32;
    public static double OpenNotchWidth => 640;
    public static double OpenNotchHeight => 190;
    public static double ShadowPadding => 20;

    public void Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(StoragePath));
            var root = doc.RootElement;
            foreach (var prop in typeof(AppSettings).GetProperties())
            {
                if (!prop.CanWrite) continue;
                if (root.TryGetProperty(prop.Name, out var el))
                {
                    var val = el.Deserialize(prop.PropertyType);
                    if (val != null) prop.SetValue(this, val);
                }
            }
        }
        catch
        {
            // Corrupt settings — fall back to defaults.
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(StorageDir);
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // ignore persistence failures
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
