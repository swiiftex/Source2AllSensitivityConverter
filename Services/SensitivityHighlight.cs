using System.Globalization;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Pure helpers for the "highlight the sensitivity value" step in a plain-text config: classify the
/// selected number and turn the line it sits on into a <c>{value}</c> template for TemplateApplier.
/// </summary>
public static partial class SensitivityHighlight
{
    [GeneratedRegex(@"^-?\d+$")] private static partial Regex Integer();
    [GeneratedRegex(@"^-?\d*\.\d+$")] private static partial Regex Decimal();
    [GeneratedRegex(@"-?\d+(\.\d+)?")] private static partial Regex NumberOnLine();

    /// <summary>
    /// Find the sensitivity value to pre-select when a file is opened: the first number on the line
    /// containing "Sensitivity" (preferring "MouseSensitivity"), or the keyword itself if no number
    /// is on that line. Returns false if "Sensitivity" doesn't appear at all.
    /// </summary>
    public static bool TryFindSensitivitySpan(string text, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (string.IsNullOrEmpty(text)) return false;

        var keyword = "MouseSensitivity";
        var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) { keyword = "Sensitivity"; idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase); }
        if (idx < 0) return false;

        var lineStart = text.LastIndexOf('\n', Math.Max(0, idx - 1)) + 1;
        var lineEnd = text.IndexOf('\n', idx);
        if (lineEnd < 0) lineEnd = text.Length;

        var keywordInLine = idx - lineStart;
        var rest = text[(lineStart + keywordInLine)..lineEnd];
        var m = NumberOnLine().Match(rest);
        if (m.Success) { start = lineStart + keywordInLine + m.Index; length = m.Length; }
        else { start = idx; length = keyword.Length; }   // no number on the line -> select the keyword
        return true;
    }

    /// <summary>"integer" / "decimal", or null if the text isn't a plain number.</summary>
    public static string? Classify(string selection)
    {
        selection = selection.Trim();
        if (Integer().IsMatch(selection)) return "integer";
        if (Decimal().IsMatch(selection)) return "decimal";
        return null;
    }

    public static bool IsNumber(string s) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// From the full text and the highlighted span, build a one-line template with the selected
    /// number replaced by <c>{value}</c> (e.g. highlighting 11 in <c>sensitivity "11.0"</c> on its
    /// line yields <c>sensitivity "{value}"</c>). Returns false if the selection isn't a number.
    /// </summary>
    public static bool TryBuildTemplate(string fullText, int selStart, int selLength,
        out string template, out string value)
    {
        template = "";
        value = "";
        if (selStart < 0 || selLength <= 0 || selStart + selLength > fullText.Length) return false;

        value = fullText.Substring(selStart, selLength).Trim();
        if (Classify(value) is null) return false;

        var lineStart = fullText.LastIndexOf('\n', Math.Max(0, selStart - 1)) + 1;
        var lineEnd = fullText.IndexOf('\n', selStart);
        if (lineEnd < 0) lineEnd = fullText.Length;
        var line = fullText[lineStart..lineEnd].TrimEnd('\r');

        var valStartInLine = selStart - lineStart;
        var valEndInLine = valStartInLine + selLength;
        if (valStartInLine < 0 || valEndInLine > line.Length) return false;

        template = line[..valStartInLine] + "{value}" + line[valEndInLine..];
        return template.Contains("{value}");
    }
}
