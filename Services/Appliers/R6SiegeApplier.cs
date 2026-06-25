using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to Rainbow Six Siege's <c>GameSettings.ini</c> <c>[INPUT]</c> section.
///
/// R6 splits sensitivity across two values: <c>MouseYawSensitivity</c> (the integer slider) and
/// <c>MouseSensitivityMultiplierUnit</c> (a fine multiplier, default 0.02). The effective sens is
/// <c>Yaw * (Multiplier / 0.02)</c>. To keep full precision we pin Yaw/Pitch to a fixed integer
/// baseline (50) and carry the exact value in the float multiplier:
///   Multiplier = 0.02 * effective / 50  =  0.0004 * effective
/// where <c>effective</c> is the converted value (R6 yaw 0.00572958 at multiplier 0.02).
/// </summary>
public sealed class R6SiegeApplier(Func<string, string?> pathResolver) : IConfigApplier
{
    private const int Baseline = 50;        // fixed MouseYaw/PitchSensitivity
    private const double RefMultiplier = 0.02;

    public string TargetDescription =>
        "Documents/My Games/Rainbow Six - Siege/<id>/GameSettings.ini  ([INPUT] MouseYaw/Pitch + MultiplierUnit)";

    public ApplyResult Apply(string installPath, double effectiveSensitivity)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path))
                return ApplyResult.Fail("Config location could not be resolved.");
            if (!File.Exists(path))
                return ApplyResult.Fail($"GameSettings.ini not found (launch the game once first): {path}");

            var multiplier = RefMultiplier * effectiveSensitivity / Baseline;

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* best effort */ }

            var text = File.ReadAllText(path);
            text = SetKey(text, "MouseYawSensitivity", Baseline.ToString(CultureInfo.InvariantCulture));
            text = SetKey(text, "MousePitchSensitivity", Baseline.ToString(CultureInfo.InvariantCulture));
            text = SetKey(text, "MouseSensitivityMultiplierUnit",
                multiplier.ToString("0.000000", CultureInfo.InvariantCulture));
            File.WriteAllText(path, text);

            return ApplyResult.Ok(
                $"Set MouseYaw/Pitch=50, MultiplierUnit={multiplier.ToString("0.000000", CultureInfo.InvariantCulture)} in {path}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write GameSettings.ini: {ex.Message}");
        }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var text = File.ReadAllText(path);
            var yaw = ReadKey(text, "MouseYawSensitivity");
            var mult = ReadKey(text, "MouseSensitivityMultiplierUnit");
            if (yaw is null || mult is null) return null;

            // Recover the effective sensitivity (what the catalog yaw expects).
            return yaw.Value * (mult.Value / RefMultiplier);
        }
        catch { return null; }
    }

    private static string SetKey(string text, string key, string value)
    {
        var rx = new Regex($@"^(\s*{Regex.Escape(key)}\s*=).*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return rx.IsMatch(text)
            ? rx.Replace(text, $"${{1}}{value}", 1)
            : text.TrimEnd() + Environment.NewLine + $"{key}={value}" + Environment.NewLine;
    }

    private static double? ReadKey(string text, string key)
    {
        var m = new Regex($@"^\s*{Regex.Escape(key)}\s*=\s*(?<v>-?[0-9]*\.?[0-9]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline).Match(text);
        return m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
