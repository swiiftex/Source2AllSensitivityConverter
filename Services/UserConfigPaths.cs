using System.IO;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Resolves per-user config file locations that live outside the game's install directory
/// (Saved Games / Documents / LocalAppData). Each resolver returns the first candidate that
/// exists; if none exist it returns the primary candidate so callers can show a helpful
/// "launch the game once" message pointing at the right place.
/// </summary>
public static class UserConfigPaths
{
    private static string UserProfile =>
        Environment.GetEnvironmentVariable("USERPROFILE")
        ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string SavedGames(params string[] parts) =>
        Path.Combine(new[] { UserProfile, "Saved Games" }.Concat(parts).ToArray());

    private static string Documents(params string[] parts) =>
        Path.Combine(new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
            .Concat(parts).ToArray());

    private static string LocalAppData(params string[] parts) =>
        Path.Combine(new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }
            .Concat(parts).ToArray());

    private static string AppData(params string[] parts) =>
        Path.Combine(new[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) }
            .Concat(parts).ToArray());

    /// <summary>Returns the first existing candidate, or the first candidate if none exist.</summary>
    private static string FirstExistingOrPrimary(params string[] candidates)
        => candidates.FirstOrDefault(File.Exists) ?? candidates[0];

    public static string Apex(string _) =>
        SavedGames("Respawn", "Apex", "local", "settings.cfg");

    public static string Titanfall2(string _) => FirstExistingOrPrimary(
        Documents("Respawn", "Titanfall2", "local", "settings.cfg"),
        SavedGames("Respawn", "Titanfall2", "local", "settings.cfg"));

    public static string QuakeChampions(string _) =>
        LocalAppData("id Software", "Quake Champions", "client", "config", "input.cfg");

    public static string DoomEternal(string _) =>
        SavedGames("id Software", "DOOMEternal", "base", "DOOMEternalConfig.local");

    public static string Mordhau(string _) =>
        LocalAppData("Mordhau", "Saved", "Config", "WindowsClient", "Input.ini");

    public static string TheFinals(string _) =>
        LocalAppData("Discovery", "Saved", "SaveGames", "EmbarkOptionSaveGame.sav");

    /// <summary>
    /// Locate a Call of Duty config file. Classic IW games (CoD4/CoD2) keep it in
    /// <c>&lt;install&gt;/players/</c>; the WaW-era games keep per-profile configs in
    /// <c>%LOCALAPPDATA%/Activision/&lt;folder&gt;/players/profiles/&lt;profile&gt;/</c>. We return the
    /// first existing match (preferring the most recently used profile), or the install path as a
    /// helpful default for the "launch the game once first" message.
    /// </summary>
    public static string CodPlayersConfig(string installPath, string fileName, params string[] activisionFolders)
    {
        var inInstall = Path.Combine(installPath, "players", fileName);
        try
        {
            if (File.Exists(inInstall)) return inInstall;

            foreach (var folder in activisionFolders)
            {
                var profiles = LocalAppData("Activision", folder, "players", "profiles");
                if (!Directory.Exists(profiles)) continue;

                var hit = Directory.EnumerateDirectories(profiles)
                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                    .Select(d => Path.Combine(d, fileName))
                    .FirstOrDefault(File.Exists);
                if (hit is not null) return hit;
            }
        }
        catch { /* fall through to default */ }
        return inInstall;
    }

    public static string MinecraftOptions(string installPath) =>
        // The scanner passes the .minecraft folder as the install path; options.txt sits inside it.
        string.IsNullOrEmpty(installPath)
            ? AppData(".minecraft", "options.txt")
            : Path.Combine(installPath, "options.txt");

    /// <summary>
    /// Rainbow Six Siege keeps settings under Documents/My Games/Rainbow Six - Siege/&lt;hash&gt;/.
    /// The hash folder is per Ubisoft account, so we pick the first one that has a GameSettings.ini.
    /// </summary>
    public static string RainbowSixSiege(string _)
    {
        var baseDir = Documents("My Games", "Rainbow Six - Siege");
        var placeholder = Path.Combine(baseDir, "<account-id>", "GameSettings.ini");
        if (!Directory.Exists(baseDir)) return placeholder;
        try
        {
            var hit = Directory.EnumerateDirectories(baseDir)
                .Select(d => Path.Combine(d, "GameSettings.ini"))
                .FirstOrDefault(File.Exists);
            return hit ?? placeholder;
        }
        catch { return placeholder; }
    }
}
