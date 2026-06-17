using System.IO;
using Microsoft.Win32;
using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services.Vdf;

namespace Source2AllSensitivityConverter.Services.Scanners;

/// <summary>
/// Finds Steam games by locating the Steam install (registry), enumerating library folders
/// from <c>libraryfolders.vdf</c>, and reading each <c>appmanifest_*.acf</c>.
/// </summary>
public sealed class SteamScanner : IStoreScanner
{
    public Store Store => Store.Steam;

    private readonly string? _steamPath;

    public SteamScanner() => _steamPath = FindSteamPath();

    public bool IsAvailable => _steamPath is not null && Directory.Exists(_steamPath);

    public IEnumerable<DetectedGame> Scan()
    {
        if (!IsAvailable) yield break;

        foreach (var library in EnumerateLibraryFolders(_steamPath!))
        {
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps)) continue;

            string[] manifests;
            try { manifests = Directory.GetFiles(steamApps, "appmanifest_*.acf"); }
            catch { continue; }

            foreach (var manifest in manifests)
            {
                DetectedGame? game = TryReadManifest(manifest, steamApps);
                if (game is not null) yield return game;
            }
        }
    }

    private static DetectedGame? TryReadManifest(string manifestPath, string steamAppsDir)
    {
        try
        {
            var app = VdfParser.ParseFile(manifestPath)["AppState"];
            if (app is null) return null;

            var installDir = app.GetString("installdir");
            var name = app.GetString("name");
            if (string.IsNullOrWhiteSpace(installDir)) return null;

            var fullPath = Path.Combine(steamAppsDir, "common", installDir);
            if (!Directory.Exists(fullPath)) return null;

            int? appId = int.TryParse(app.GetString("appid"), out var id) ? id : null;

            return new DetectedGame
            {
                DisplayName = string.IsNullOrWhiteSpace(name) ? installDir : name!,
                InstallPath = fullPath,
                Store = Store.Steam,
                SteamAppId = appId,
            };
        }
        catch { return null; }
    }

    /// <summary>Reads library paths from libraryfolders.vdf (handles both old and new formats).</summary>
    private static IEnumerable<string> EnumerateLibraryFolders(string steamPath)
    {
        yield return steamPath; // the main install is always a library

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        VdfNode root;
        try { root = VdfParser.ParseFile(vdf); }
        catch { yield break; }

        var folders = root["libraryfolders"] ?? root["LibraryFolders"];
        if (folders is null) yield break;

        foreach (var (_, node) in folders.Children)
        {
            // New format: each numbered entry is an object with a "path".
            // Old format: each numbered entry is a string value that is the path.
            var path = node.GetString("path") ?? node.Value;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                yield return path!;
        }
    }

    private static string? FindSteamPath()
    {
        // Current user first, then machine-wide (32-bit view holds WOW6432Node).
        var p = ReadRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        if (p is not null) return Normalize(p);

        p = ReadRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", RegistryView.Registry32);
        if (p is not null) return Normalize(p);

        p = ReadRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        return p is null ? null : Normalize(p);
    }

    private static string Normalize(string path) => path.Replace('/', '\\');

    private static string? ReadRegistry(RegistryKey hive, string subKey, string value,
        RegistryView view = RegistryView.Default)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive.Name.StartsWith("HKEY_CURRENT")
                ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(value) as string;
        }
        catch { return null; }
    }
}
