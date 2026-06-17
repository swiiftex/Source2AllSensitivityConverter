using System.IO;
using Microsoft.Win32;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services.Scanners;

/// <summary>
/// Finds GOG / GOG Galaxy installs from the registry under
/// <c>HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\&lt;id&gt;</c> (read via the 32-bit view).
/// </summary>
public sealed class GogScanner : IStoreScanner
{
    public Store Store => Store.Gog;

    public bool IsAvailable
    {
        get
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var games = baseKey.OpenSubKey(@"SOFTWARE\GOG.com\Games");
                return games is not null;
            }
            catch { return false; }
        }
    }

    public IEnumerable<DetectedGame> Scan()
    {
        RegistryKey? baseKey = null, games = null;
        try
        {
            baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            games = baseKey.OpenSubKey(@"SOFTWARE\GOG.com\Games");
        }
        catch { /* ignore */ }

        if (games is null)
        {
            baseKey?.Dispose();
            yield break;
        }

        foreach (var id in SafeSubKeyNames(games))
        {
            DetectedGame? game = TryRead(games, id);
            if (game is not null) yield return game;
        }

        games.Dispose();
        baseKey?.Dispose();
    }

    private static string[] SafeSubKeyNames(RegistryKey key)
    {
        try { return key.GetSubKeyNames(); }
        catch { return []; }
    }

    private static DetectedGame? TryRead(RegistryKey games, string id)
    {
        try
        {
            using var g = games.OpenSubKey(id);
            if (g is null) return null;

            var path = g.GetValue("path") as string ?? g.GetValue("exe") as string;
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (File.Exists(path)) path = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return null;

            var name = g.GetValue("gameName") as string ?? Path.GetFileName(path);

            return new DetectedGame
            {
                DisplayName = string.IsNullOrWhiteSpace(name) ? id : name!,
                InstallPath = path!,
                Store = Store.Gog,
            };
        }
        catch { return null; }
    }
}
