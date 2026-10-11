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

            foreach (var game in found.SelectMany(ExpandHosted))
            {
                if (game.Definition is null) Enrich(game);

                // De-dupe by install path + matched game: one folder can hold several distinct games
                // (e.g. every GoldSrc title lives in "Half-Life"), while separate SP/MP app ids for
                // the same game collapse into a single row.
                var key = Path.GetFullPath(game.InstallPath).TrimEnd('\\') + "|" + (game.Definition?.Name ?? "");
                if (!seen.Add(key)) continue;

                results.Add(game);
            }
        }

        return results
            .OrderByDescending(g => g.CanAutoApply)
            .ThenByDescending(g => g.CanConvert)
            .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// A launcher install that hosts several games (Call of Duty HQ) becomes one row per hosted game,
    /// so each gets its own conversion and config writer. Anything else passes through unchanged.
    /// </summary>
    private static IEnumerable<DetectedGame> ExpandHosted(DetectedGame game)
    {
        var hosted = game.SteamAppId is int id ? GameCatalog.HostedBy(id) : [];
        if (hosted.Count == 0) return [game];

        // Only filter when the manifest actually reports DLC; otherwise show every hosted game.
        var installed = game.InstalledDlcAppIds;
        var shown = installed.Length == 0
            ? hosted
            : hosted.Where(d => d.HostDlcAppIds.Length == 0 || d.HostDlcAppIds.Intersect(installed).Any()).ToList();
        if (shown.Count == 0) shown = hosted;

        return shown.Select(def => new DetectedGame
        {
            DisplayName = def.Name,
            InstallPath = game.InstallPath,
            Store = game.Store,
            SteamAppId = game.SteamAppId,
            InstalledDlcAppIds = installed,
            Definition = def,
            DetectedEngine = def.Engine,
        });
    }

    /// <summary>Attach a catalog definition and/or detected engine to a raw install.</summary>
    private static void Enrich(DetectedGame game)
    {
        var def = game.SteamAppId is int id ? GameCatalog.MatchByAppId(id) : null;

        // A name-based match is only accepted if its marker files actually exist on disk, to avoid
        // false positives from generic folder names (including Steam apps the catalog doesn't list).
        if (def is null)
        {
            def = GameCatalog.MatchByName(game.DisplayName) ?? GameCatalog.MatchByName(game.InstallPath);
            if (def is not null && !MarkersPresent(def, game.InstallPath))
                def = null;
        }

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
