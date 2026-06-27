using System.IO;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to an early-IW Call of Duty game by writing <c>seta sensitivity "x"</c> to
/// every SP/MP config across all of its profiles (config.cfg + config_mp.cfg). Config locations
/// differ per game and a player can have several profiles, so this writes them all. Each file is
/// edited via <see cref="CvarApplier"/> (replace-in-place, append if missing, one-time backup).
/// </summary>
public sealed class CodSensitivityApplier(params string[] activisionFolders) : IConfigApplier
{
    public string TargetDescription =>
        "players[/profiles]/config.cfg + config_mp.cfg (all profiles)  →  seta sensitivity";

    private static CvarApplier WriterFor(string path) =>
        new($"{path}", _ => path, "sensitivity", setaStyle: true);

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        var files = UserConfigPaths.CodConfigFiles(installPath, activisionFolders);
        if (files.Count == 0)
            return ApplyResult.Fail(
                $"No CoD config found (launch the game once first): {Path.Combine(installPath, "players")}");

        int ok = 0;
        var problems = new List<string>();
        foreach (var file in files)
        {
            var r = WriterFor(file).Apply(installPath, sensitivity);
            if (r.Success) ok++; else problems.Add(r.Message);
        }

        return ok > 0
            ? ApplyResult.Ok($"Set sensitivity in {ok} config file(s) across all profiles.")
            : ApplyResult.Fail(string.Join(" | ", problems));
    }

    public double? TryReadCurrent(string installPath)
    {
        foreach (var file in UserConfigPaths.CodConfigFiles(installPath, activisionFolders))
            if (WriterFor(file).TryReadCurrent(installPath) is { } v)
                return v;
        return null;
    }
}
