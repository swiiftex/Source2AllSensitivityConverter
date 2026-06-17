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
}
