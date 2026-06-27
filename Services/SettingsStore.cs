using System.IO;
using System.Text.Json;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services;

/// <summary>Small user-preferences blob persisted between runs.</summary>
public sealed class AppSettings
{
    // Convert tab
    public string? FromGame { get; set; }
    public string? ToGame { get; set; }
    public string? Sensitivity { get; set; }

    // Auto-apply tab input (remembered so you can come back and apply to another game quickly)
    public string? AutoFromGame { get; set; }
    public string? AutoSensitivity { get; set; }
    public bool AutoByCm360 { get; set; }
    public string? Cm360 { get; set; }
    public string? Dpi { get; set; }

    /// <summary>Games the user added manually via the "Manually add game" dialog.</summary>
    public List<CustomGame> CustomGames { get; set; } = [];
}

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON. Portable-first: settings live in a
/// <c>UserData</c> folder next to the executable when that location is writable (so the app can be
/// moved around with its settings), otherwise under
/// <c>%LOCALAPPDATA%\Source2AllSensitivityConverter</c>. Older settings from a previous location are
/// migrated automatically. All operations are best-effort.
/// </summary>
public static class SettingsStore
{
    private static readonly string FilePath = ResolvePath();

    private static string PortablePath =>
        Path.Combine(AppContext.BaseDirectory, "UserData", "settings.json");

    private static string LocalPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Source2AllSensitivityConverter", "settings.json");

    private static string RoamingPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Source2AllSensitivityConverter", "settings.json");

    private static string ResolvePath()
    {
        // Prefer a writable folder next to the exe (portable); fall back to LocalAppData.
        try
        {
            var dir = Path.GetDirectoryName(PortablePath)!;
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return PortablePath;
        }
        catch { return LocalPath; }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Deserialize(FilePath);

            // Migrate from a previous location, if any.
            foreach (var legacy in new[] { LocalPath, RoamingPath })
            {
                if (legacy != FilePath && File.Exists(legacy))
                {
                    var migrated = Deserialize(legacy);
                    Save(migrated);
                    return migrated;
                }
            }
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }

    private static AppSettings Deserialize(string path)
        => JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
}
