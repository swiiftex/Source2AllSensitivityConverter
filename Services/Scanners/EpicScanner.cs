using System.IO;
using System.Text.Json;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services.Scanners;

/// <summary>
/// Finds Epic Games Launcher installs by reading the per-app manifest JSON files under
/// <c>%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>.
/// </summary>
public sealed class EpicScanner : IStoreScanner
{
    public Store Store => Store.Epic;

    private readonly string _manifestDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public bool IsAvailable => Directory.Exists(_manifestDir);

    public IEnumerable<DetectedGame> Scan()
    {
        if (!IsAvailable) yield break;

        string[] items;
        try { items = Directory.GetFiles(_manifestDir, "*.item"); }
        catch { yield break; }

        foreach (var item in items)
        {
            DetectedGame? game = TryRead(item);
            if (game is not null) yield return game;
        }
    }

    private static DetectedGame? TryRead(string itemPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(itemPath));
            var root = doc.RootElement;

            var installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
            if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
                return null;

            var name = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
            name ??= root.TryGetProperty("AppName", out var an) ? an.GetString() : null;

            return new DetectedGame
            {
                DisplayName = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(installLocation!) : name!,
                InstallPath = installLocation!,
                Store = Store.Epic,
            };
        }
        catch { return null; }
    }
}
