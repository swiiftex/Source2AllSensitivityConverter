using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to Minecraft (Java) by setting <c>mouseSensitivity:&lt;value&gt;</c> in
/// <c>%APPDATA%/.minecraft/options.txt</c>. options.txt uses <c>key:value</c> lines (one per line);
/// the in-game 100% slider corresponds to 0.5 here, and the value is the 0–1 figure we convert to.
/// </summary>
public sealed class MinecraftOptionsApplier(Func<string, string?> pathResolver) : IConfigApplier
{
    private static readonly Regex Line = new(
        @"^(\s*mouseSensitivity\s*:\s*)(-?[0-9]*\.?[0-9]+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public string TargetDescription => "%APPDATA%/.minecraft/options.txt  (mouseSensitivity)";

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path))
                return ApplyResult.Fail("options.txt location could not be resolved.");
            if (!File.Exists(path))
                return ApplyResult.Fail($"options.txt not found (launch Minecraft once first): {path}");

            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* best effort */ }

            var text = File.ReadAllText(path);
            text = Line.IsMatch(text)
                ? Line.Replace(text, $"${{1}}{value}", 1)
                : text.TrimEnd() + Environment.NewLine + $"mouseSensitivity:{value}" + Environment.NewLine;
            File.WriteAllText(path, text);

            return ApplyResult.Ok($"Set mouseSensitivity:{value} in {path}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write options.txt: {ex.Message}");
        }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var m = Line.Match(File.ReadAllText(path));
            return m.Success && double.TryParse(m.Groups[2].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch { return null; }
    }
}
