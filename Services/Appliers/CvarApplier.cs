using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Applies a sensitivity by setting a console-variable line (e.g. <c>sensitivity "2.5"</c> or
/// <c>seta sensitivity "2.5"</c> or <c>mouse_sensitivity "3"</c>) in an existing config file
/// outside the install directory (Saved Games / Documents / LocalAppData).
///
/// Safety: the file is located by <see cref="_pathResolver"/> and is only edited when it already
/// exists — we never fabricate a config in a guessed location. The existing cvar value is replaced
/// in place when present; otherwise the line is appended (engines ignore unknown cvars). A one-time
/// <c>*.s2a-backup</c> is taken before writing.
/// </summary>
public sealed class CvarApplier : IConfigApplier
{
    private readonly Func<string, string?> _pathResolver;
    private readonly string _cvar;
    private readonly bool _setaStyle;
    private readonly Regex _lineRegex;

    public CvarApplier(string targetDescription, Func<string, string?> pathResolver,
        string cvar, bool setaStyle = false)
    {
        TargetDescription = targetDescription;
        _pathResolver = pathResolver;
        _cvar = cvar;
        _setaStyle = setaStyle;
        // Matches: optional "seta", optionally-quoted cvar name, then an optionally-quoted number.
        _lineRegex = new Regex(
            $@"^\s*(?:seta\s+)?""?{Regex.Escape(cvar)}""?\s+""?(?<v>-?[0-9]*\.?[0-9]+)""?.*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
    }

    public string TargetDescription { get; }

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = _pathResolver(installPath);
            if (string.IsNullOrEmpty(path))
                return ApplyResult.Fail("Config file location could not be resolved.");
            if (!File.Exists(path))
                return ApplyResult.Fail($"Config not found (launch the game once first): {path}");

            var value = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);
            var line = _setaStyle ? $"seta {_cvar} \"{value}\"" : $"{_cvar} \"{value}\"";

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* backup best-effort */ }

            var text = File.ReadAllText(path);
            text = _lineRegex.IsMatch(text)
                ? _lineRegex.Replace(text, line, 1)
                : text.TrimEnd() + Environment.NewLine + line + Environment.NewLine;
            File.WriteAllText(path, text);

            return ApplyResult.Ok($"Set {_cvar} {value} in {path}");
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
            var path = _pathResolver(installPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var m = _lineRegex.Match(File.ReadAllText(path));
            if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch { /* best effort */ }
        return null;
    }
}
