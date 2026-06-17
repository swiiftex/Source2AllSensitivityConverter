using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity to a game that stores it as a <c>Key=Value</c> entry in an INI-style
/// file, typically under the user's Documents folder rather than the install directory.
/// The file is located by <paramref name="pathResolver"/>; <c>installPath</c> is passed through
/// in case the resolver needs it.
/// </summary>
public sealed class IniSensitivityApplier(
    string targetDescription,
    Func<string, string?> pathResolver,
    string key,
    string? section = null) : IConfigApplier
{
    public string TargetDescription { get; } = targetDescription;

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path))
                return ApplyResult.Fail("Config file location could not be resolved.");
            if (!File.Exists(path))
                return ApplyResult.Fail($"Config file not found (launch the game once first): {path}");

            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);
            var lines = File.ReadAllLines(path).ToList();
            var keyRegex = new Regex($@"^\s*{Regex.Escape(key)}\s*=", RegexOptions.IgnoreCase);

            var (start, end) = SectionBounds(lines, section);
            var replaced = false;
            for (var i = start; i < end; i++)
            {
                if (!keyRegex.IsMatch(lines[i])) continue;
                lines[i] = $"{key}={value}";
                replaced = true;
                break;
            }

            if (!replaced)
                lines.Insert(end, $"{key}={value}");

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* backup best-effort */ }

            File.WriteAllLines(path, lines);
            return ApplyResult.Ok($"Set {key}={value} in {path}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write config: {ex.Message}");
        }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var lines = File.ReadAllLines(path);
            var keyRegex = new Regex($@"^\s*{Regex.Escape(key)}\s*=\s*(?<v>[0-9]*\.?[0-9]+)",
                RegexOptions.IgnoreCase);
            var (start, end) = SectionBounds(lines.ToList(), section);
            for (var i = start; i < end; i++)
            {
                var m = keyRegex.Match(lines[i]);
                if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var v))
                    return v;
            }
        }
        catch { /* best effort */ }
        return null;
    }

    /// <summary>Returns the [start, end) line range covering the requested section (or the whole file).</summary>
    private static (int start, int end) SectionBounds(List<string> lines, string? section)
    {
        if (string.IsNullOrEmpty(section)) return (0, lines.Count);

        var header = $"[{section}]";
        var start = lines.FindIndex(l => l.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));
        if (start < 0) return (lines.Count, lines.Count); // not present -> append at EOF

        start++; // first line after the header
        var end = lines.FindIndex(start, l => l.TrimStart().StartsWith('['));
        return (start, end < 0 ? lines.Count : end);
    }
}
