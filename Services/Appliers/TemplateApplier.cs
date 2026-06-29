using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// A config writer driven by a one-line template containing a <c>{value}</c> placeholder, e.g.
/// <c>sensitivity "{value}"</c>, <c>seta sensitivity "{value}"</c>, <c>Sensitivity={value}</c>,
/// <c>mouseSensitivity:{value}</c>, or <c>MouseSensitivity = "{value}"</c>.
///
/// On apply we find the existing line that starts with the template's key (the text before
/// <c>{value}</c>, ignoring the trailing separators/quotes) and replace its number; otherwise the
/// filled-in line is appended. A one-time <c>*.s2a-backup</c> is taken before editing.
///
/// Used both for manually-added games (absolute path, created if missing because the user chose it)
/// and for catalog games whose config is install-relative (resolver + <c>createIfMissing: false</c>).
/// </summary>
public sealed class TemplateApplier : IConfigApplier
{
    private const string Placeholder = "{value}";

    private readonly Func<string, string?> _resolve;
    private readonly string _template;
    private readonly bool _createIfMissing;
    private readonly Regex? _findRegex;

    public TemplateApplier(string absolutePath, string template)
        : this(_ => absolutePath, template, createIfMissing: true) { }

    public TemplateApplier(Func<string, string?> pathResolver, string template, bool createIfMissing = true)
    {
        _resolve = pathResolver;
        _template = template;
        _createIfMissing = createIfMissing;

        var idx = template.IndexOf(Placeholder, StringComparison.OrdinalIgnoreCase);
        var head = idx >= 0 ? template[..idx] : template;

        // The "key" is the head with trailing separators/quotes/space stripped (e.g. `sensitivity "`
        // -> `sensitivity`, `MouseSensitivity = "` -> `MouseSensitivity`, `Sensitivity=` -> `Sensitivity`).
        var key = head.Trim().TrimEnd('=', ':', '"', '\'', ' ', '\t');
        var tokens = key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0)
        {
            var keyPattern = string.Join(@"\s+", tokens.Select(Regex.Escape));
            // Match the whole line up to (but not including) the newline. Note: no trailing `$` —
            // with CRLF files `[^\r\n]*$` fails because `$` can't match at the `\r`; the greedy
            // `[^\r\n]*` already consumes the rest of the line, which is what we replace.
            _findRegex = new Regex(
                $@"^[ \t]*{keyPattern}\b[^\r\n]*?(?<v>-?[0-9]*\.?[0-9]+)[^\r\n]*",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }

    public string TargetDescription
    {
        get { try { return $"{_resolve("")}  ({_template})"; } catch { return _template; } }
    }

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = _resolve(installPath);
            if (string.IsNullOrEmpty(path)) return ApplyResult.Fail("Config location could not be resolved.");

            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);
            var line = ReplacePlaceholder(_template, value);

            if (!File.Exists(path))
            {
                if (!_createIfMissing)
                    return ApplyResult.Fail($"Config not found (launch the game once first): {path}");

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, line + Environment.NewLine);
                return ApplyResult.Ok($"Created {path} with {line}");
            }

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* best effort */ }

            var text = File.ReadAllText(path);
            if (_findRegex is not null && _findRegex.IsMatch(text))
                text = _findRegex.Replace(text, line.Replace("$", "$$"), 1);
            else
                text = text.TrimEnd() + Environment.NewLine + line + Environment.NewLine;

            File.WriteAllText(path, text);
            return ApplyResult.Ok($"Set \"{line}\" in {path}");
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
            var path = _resolve(installPath);
            if (_findRegex is null || string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var m = _findRegex.Match(File.ReadAllText(path));
            return m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch { return null; }
    }

    private static string ReplacePlaceholder(string template, string value)
        => Regex.Replace(template, Regex.Escape(Placeholder), value.Replace("$", "$$"),
            RegexOptions.IgnoreCase);
}
