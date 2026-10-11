using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Writes a sensitivity by replacing only the number captured by the <c>v</c> group of a regex,
/// leaving everything else in the file — quotes, comments, indentation, other settings, encoding —
/// exactly as it was. Applies to every matching file (e.g. one per account/profile folder); files
/// that don't contain the key are skipped. Only existing files are edited, each backed up first.
/// </summary>
public sealed class RegexValueApplier : IConfigApplier
{
    private readonly string _description;
    private readonly string _pathPattern;
    private readonly Regex _regex;
    private readonly string _format;

    /// <param name="pathPattern">A <see cref="ConfigPathPattern"/> (tokens + wildcards allowed).</param>
    /// <param name="pattern">Regex with a named group <c>v</c> around the number to replace.</param>
    /// <param name="format">.NET number format for the written value (e.g. "0.000000").</param>
    public RegexValueApplier(string description, string pathPattern, string pattern, string format = "0.######")
    {
        _description = description;
        _pathPattern = pathPattern;
        _regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        _format = format;
    }

    public string TargetDescription => _description;

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        var files = ConfigPathPattern.Expand(_pathPattern, installPath);
        if (files.Count == 0)
            return ApplyResult.Fail(
                $"Config not found (launch the game once first): {ConfigPathPattern.Describe(_pathPattern, installPath)}");

        var value = sensitivity.ToString(_format, CultureInfo.InvariantCulture);
        int written = 0;
        var problems = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var text = TextFile.Read(file, out var encoding);
                var m = _regex.Match(text);
                if (!m.Success || !m.Groups["v"].Success) continue; // key not in this file

                var g = m.Groups["v"];
                var updated = text[..g.Index] + value + text[(g.Index + g.Length)..];

                try
                {
                    var bak = file + ".s2a-backup";
                    if (!File.Exists(bak)) File.Copy(file, bak);
                }
                catch { /* best effort */ }

                TextFile.Write(file, updated, encoding);
                written++;
            }
            catch (Exception ex)
            {
                problems.Add($"{file}: {ex.Message}");
            }
        }

        if (written > 0)
            return ApplyResult.Ok($"Set sensitivity {value} in {written} file(s).");

        return ApplyResult.Fail(problems.Count > 0
            ? string.Join(" | ", problems)
            : $"Sensitivity setting not found in {ConfigPathPattern.Describe(_pathPattern, installPath)}");
    }

    public double? TryReadCurrent(string installPath)
    {
        foreach (var file in ConfigPathPattern.Expand(_pathPattern, installPath))
        {
            try
            {
                var m = _regex.Match(TextFile.Read(file, out _));
                if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch { /* try the next file */ }
        }
        return null;
    }
}
