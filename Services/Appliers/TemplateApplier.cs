using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// A user-defined config writer for manually added games. The user supplies the absolute config
/// file path and a one-line template containing a <c>{value}</c> placeholder, e.g.
/// <c>sensitivity "{value}"</c>, <c>seta sensitivity "{value}"</c>, <c>Sensitivity={value}</c> or
/// <c>mouseSensitivity:{value}</c>.
///
/// On apply we look for an existing line that starts with the template's key (the text before
/// <c>{value}</c>, ignoring the trailing quote/separator) and replace its number; otherwise the
/// filled-in line is appended. The file is created if it doesn't exist (the user explicitly chose it).
/// A one-time <c>*.s2a-backup</c> is taken before editing an existing file.
/// </summary>
public sealed class TemplateApplier : IConfigApplier
{
    private const string Placeholder = "{value}";

    private readonly string _path;
    private readonly string _template;
    private readonly Regex? _findRegex;

    public TemplateApplier(string absolutePath, string template)
    {
        _path = absolutePath;
        _template = template;

        var idx = template.IndexOf(Placeholder, StringComparison.OrdinalIgnoreCase);
        var head = idx >= 0 ? template[..idx] : template;

        // The "key" is the head with trailing separators/quotes/space stripped (e.g. `sensitivity "`
        // -> `sensitivity`, `Sensitivity=` -> `Sensitivity`, `seta sensitivity "` -> `seta sensitivity`).
        var key = head.Trim().TrimEnd('=', ':', '"', '\'', ' ', '\t');
        var tokens = key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0)
        {
            var keyPattern = string.Join(@"\s+", tokens.Select(Regex.Escape));
            _findRegex = new Regex(
                $@"^[ \t]*{keyPattern}\b[^\r\n]*?(?<v>-?[0-9]*\.?[0-9]+)[^\r\n]*$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }

    public string TargetDescription => $"{_path}  ({_template})";

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);
            var line = ReplacePlaceholder(_template, value);

            if (!File.Exists(_path))
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, line + Environment.NewLine);
                return ApplyResult.Ok($"Created {_path} with {line}");
            }

            try
            {
                var bak = _path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(_path, bak);
            }
            catch { /* best effort */ }

            var text = File.ReadAllText(_path);
            if (_findRegex is not null && _findRegex.IsMatch(text))
                text = _findRegex.Replace(text, line.Replace("$", "$$"), 1);
            else
                text = text.TrimEnd() + Environment.NewLine + line + Environment.NewLine;

            File.WriteAllText(_path, text);
            return ApplyResult.Ok($"Set \"{line}\" in {_path}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write {_path}: {ex.Message}");
        }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            if (_findRegex is null || !File.Exists(_path)) return null;
            var m = _findRegex.Match(File.ReadAllText(_path));
            return m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch { return null; }
    }

    private static string ReplacePlaceholder(string template, string value)
        => Regex.Replace(template, Regex.Escape(Placeholder), value.Replace("$", "$$"),
            RegexOptions.IgnoreCase);
}
