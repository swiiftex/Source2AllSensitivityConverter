using System.IO;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Expands config path patterns into the files that exist on disk. Patterns may use the tokens
/// <c>{Install}</c>, <c>{Documents}</c>, <c>{SavedGames}</c>, <c>{LocalAppData}</c>, <c>{AppData}</c>,
/// <c>{LocalLow}</c> and <c>{Steam}</c>, plus <c>*</c>/<c>?</c> wildcards in any segment — e.g.
/// <c>{Documents}\Call of Duty MWII\players\*\settings.*.local.cod22.cst</c> matches every account folder.
/// </summary>
public static class ConfigPathPattern
{
    public static IReadOnlyList<string> Expand(string pattern, string installPath)
    {
        var path = Substitute(pattern, installPath);
        var parts = path.Split('\\', '/');
        var first = Array.FindIndex(parts, HasWildcard);
        if (first < 0)
            return File.Exists(path) ? [path] : [];

        var baseDir = string.Join('\\', parts.Take(first));
        if (baseDir.EndsWith(':')) baseDir += '\\';

        var results = new List<string>();
        try { Walk(baseDir, parts, first, results); }
        catch { /* unreadable folders: return what we have */ }
        return results;
    }

    /// <summary>The pattern with tokens substituted, for "where to look" messages.</summary>
    public static string Describe(string pattern, string installPath) => Substitute(pattern, installPath);

    private static void Walk(string dir, string[] parts, int index, List<string> results)
    {
        if (!Directory.Exists(dir)) return;
        var isLast = index == parts.Length - 1;
        var entries = isLast
            ? Directory.EnumerateFiles(dir, parts[index])
            : Directory.EnumerateDirectories(dir, parts[index]);

        foreach (var entry in entries)
        {
            if (isLast) { results.Add(entry); continue; }

            // Append literal segments until the next wildcard segment (or the end).
            var next = index + 1;
            var current = entry;
            while (next < parts.Length && !HasWildcard(parts[next]))
                current = Path.Combine(current, parts[next++]);

            if (next == parts.Length)
            {
                if (File.Exists(current)) results.Add(current);
            }
            else
            {
                Walk(current, parts, next, results);
            }
        }
    }

    private static bool HasWildcard(string segment) => segment.Contains('*') || segment.Contains('?');

    private static string Substitute(string pattern, string installPath)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return pattern
            .Replace("{Install}", installPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{Documents}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase)
            .Replace("{SavedGames}", Path.Combine(profile, "Saved Games"), StringComparison.OrdinalIgnoreCase)
            .Replace("{LocalAppData}", localAppData, StringComparison.OrdinalIgnoreCase)
            .Replace("{AppData}", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase)
            .Replace("{LocalLow}", Path.Combine(Path.GetDirectoryName(localAppData) ?? profile, "LocalLow"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Steam}", SteamRoot.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Lazy<string> SteamRoot = new(() =>
        Scanners.SteamScanner.FindSteamPath()
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
}
