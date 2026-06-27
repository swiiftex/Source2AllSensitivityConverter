using System.IO;
using System.Text.Json;

namespace Source2AllSensitivityConverter.Services;

/// <summary>Small user-preferences blob persisted between runs.</summary>
public sealed class AppSettings
{
    public string? FromGame { get; set; }
    public string? ToGame { get; set; }
    public string? Sensitivity { get; set; }
}

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON under
/// <c>%APPDATA%\Source2AllSensitivityConverter\settings.json</c>. All operations are best-effort:
/// a missing or corrupt file just yields defaults, and write failures are swallowed.
/// </summary>
public static class SettingsStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Source2AllSensitivityConverter");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}
