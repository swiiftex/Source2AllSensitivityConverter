using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to a Source / Source 2 / GoldSrc game by writing a <c>sensitivity</c>
/// command into <c>&lt;install&gt;/&lt;modFolder&gt;/&lt;cfgSubdir&gt;/autoexec.cfg</c>. autoexec runs on
/// game start, overriding config.cfg without us editing the game's own managed config.
///
/// Source/Source 2 keep configs under a <c>cfg</c> subfolder; GoldSrc games keep autoexec.cfg in
/// the mod folder root, so <paramref name="cfgSubdir"/> can be set to "" for those.
/// </summary>
public sealed partial class SourceCfgApplier(string modFolder, string cfgSubdir = "cfg") : IConfigApplier
{
    private readonly string _modFolder = modFolder;
    private readonly string _cfgSubdir = cfgSubdir;

    public string TargetDescription =>
        $"{CfgRelative}/autoexec.cfg  (sensitivity \"<value>\")";

    private string CfgRelative => string.IsNullOrEmpty(_cfgSubdir)
        ? _modFolder
        : $"{_modFolder}/{_cfgSubdir}";

    [GeneratedRegex(@"^\s*sensitivity\s+""?(?<v>[0-9]*\.?[0-9]+)""?",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SensitivityLine();

    private string CfgDir(string installPath) => string.IsNullOrEmpty(_cfgSubdir)
        ? Path.Combine(installPath, _modFolder)
        : Path.Combine(installPath, _modFolder, _cfgSubdir);

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var cfgDir = CfgDir(installPath);
            Directory.CreateDirectory(cfgDir);
            var autoexec = Path.Combine(cfgDir, "autoexec.cfg");

            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);
            var line = $"sensitivity \"{value}\"";

            if (File.Exists(autoexec))
            {
                BackupOnce(autoexec);
                var text = File.ReadAllText(autoexec);
                text = SensitivityLine().IsMatch(text)
                    ? SensitivityLine().Replace(text, line, 1)
                    : text.TrimEnd() + Environment.NewLine + line + Environment.NewLine;
                File.WriteAllText(autoexec, text);
            }
            else
            {
                File.WriteAllText(autoexec,
                    "// Written by Source2AllSensitivityConverter" + Environment.NewLine +
                    line + Environment.NewLine);
            }

            return ApplyResult.Ok($"Set sensitivity {value} in {autoexec}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write autoexec.cfg: {ex.Message}");
        }
    }

    /// <summary>Keep a one-time pristine backup so the user can always revert our edits.</summary>
    private static void BackupOnce(string path)
    {
        try
        {
            var bak = path + ".s2a-backup";
            if (!File.Exists(bak)) File.Copy(path, bak);
        }
        catch { /* backup is best-effort, never block the apply */ }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            var cfgDir = CfgDir(installPath);
            foreach (var name in new[] { "autoexec.cfg", "config.cfg" })
            {
                var path = Path.Combine(cfgDir, name);
                if (!File.Exists(path)) continue;
                var m = SensitivityLine().Match(File.ReadAllText(path));
                if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var v))
                    return v;
            }
        }
        catch { /* best effort */ }
        return null;
    }
}
