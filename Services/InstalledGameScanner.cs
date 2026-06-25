using System.IO;
using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services.Scanners;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Runs every store scanner, de-duplicates installs, matches each one against the catalog,
/// and falls back to file-structure engine detection for anything unknown.
/// </summary>
public sealed class InstalledGameScanner
{
    private readonly IReadOnlyList<IStoreScanner> _scanners;

    public InstalledGameScanner(IEnumerable<IStoreScanner>? scanners = null)
        => _scanners = (scanners ?? DefaultScanners()).ToList();

    public static IEnumerable<IStoreScanner> DefaultScanners() =>
    [
        new SteamScanner(),
        new EpicScanner(),
        new GogScanner(),
        new MinecraftScanner(),
    ];

    public IReadOnlyList<Store> AvailableStores =>
        _scanners.Where(s => s.IsAvailable).Select(s => s.Store).ToList();

    /// <summary>Scans all stores. Safe to call on a background thread.</summary>
    public List<DetectedGame> Scan()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<DetectedGame>();

        foreach (var scanner in _scanners)
        {
            if (!scanner.IsAvailable) continue;

            IEnumerable<DetectedGame> found;
            try { found = scanner.Scan(); }
            catch { continue; }

            foreach (var game in found)
            {
                // De-dupe by install path (same game can appear in overlapping libraries).
                var key = Path.GetFullPath(game.InstallPath).TrimEnd('\\');
                if (!seen.Add(key)) continue;

                Enrich(game);
                results.Add(game);
            }
        }

        return results
            .OrderByDescending(g => g.CanAutoApply)
            .ThenByDescending(g => g.CanConvert)
            .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Attach a catalog definition and/or detected engine to a raw install.</summary>
    private static void Enrich(DetectedGame game)
    {
        var def = (game.SteamAppId is int id ? GameCatalog.MatchByAppId(id) : null)
                  ?? GameCatalog.MatchByName(game.DisplayName)
                  ?? GameCatalog.MatchByName(game.InstallPath);

        // Only accept a name-based match if its marker files actually exist on disk, to avoid
        // false positives from generic folder names.
        if (def is not null && game.SteamAppId is null && !MarkersPresent(def, game.InstallPath))
            def = null;

        game.Definition = def;
        game.DetectedEngine = def?.Engine is { } e and not Engine.Unknown
            ? e
            : EngineDetector.Detect(game.InstallPath);
    }

    private static bool MarkersPresent(GameDefinition def, string installPath)
    {
        if (def.MarkerFiles.Length == 0) return true;
        try
        {
            return def.MarkerFiles.Any(rel =>
            {
                var full = Path.Combine(installPath, rel.Replace('/', '\\'));
                return File.Exists(full) || Directory.Exists(full);
            });
        }
        catch { return false; }
    }
}
