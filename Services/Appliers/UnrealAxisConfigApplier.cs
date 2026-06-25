using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to an Unreal Engine game that stores per-axis sensitivity as
/// <c>AxisConfig=(AxisKeyName="MouseX",AxisProperties=(…,Sensitivity=0.07,…))</c> in Input.ini
/// (e.g. Mordhau). Both MouseX (yaw) and MouseY (pitch) are set to the converted value so the
/// horizontal feel matches; vertical becomes 1:1.
///
/// Safety: edits the file only if it exists and the MouseX axis entry is present (so it never
/// fabricates a malformed AxisConfig), and backs up first.
/// </summary>
public sealed class UnrealAxisConfigApplier(string targetDescription, Func<string, string?> pathResolver)
    : IConfigApplier
{
    public string TargetDescription { get; } = targetDescription;

    private static Regex AxisRegex(string axis) => new(
        $"(AxisKeyName=\"{axis}\".*?Sensitivity=)(-?[0-9]*\\.?[0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path))
                return ApplyResult.Fail("Config location could not be resolved.");
            if (!File.Exists(path))
                return ApplyResult.Fail($"Input.ini not found (launch the game once first): {path}");

            var text = File.ReadAllText(path);
            if (!AxisRegex("MouseX").IsMatch(text))
                return ApplyResult.Fail("MouseX AxisConfig not found in Input.ini — config format not recognised.");

            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* best effort */ }

            text = AxisRegex("MouseX").Replace(text, $"${{1}}{value}", 1);
            if (AxisRegex("MouseY").IsMatch(text))
                text = AxisRegex("MouseY").Replace(text, $"${{1}}{value}", 1);
            File.WriteAllText(path, text);

            return ApplyResult.Ok($"Set MouseX/MouseY Sensitivity={value} in {path}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write Input.ini: {ex.Message}");
        }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var m = AxisRegex("MouseX").Match(File.ReadAllText(path));
            return m.Success && double.TryParse(m.Groups[2].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch { return null; }
    }
}
