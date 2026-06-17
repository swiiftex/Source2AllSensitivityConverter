using System.IO;
using System.Text;

namespace Source2AllSensitivityConverter.Services.Vdf;

/// <summary>
/// A node in a Valve KeyValues (.vdf / .acf) document. Each node is either a leaf with a
/// string <see cref="Value"/> or a container with named <see cref="Children"/>.
/// </summary>
public sealed class VdfNode
{
    public string? Value { get; set; }

    public Dictionary<string, VdfNode> Children { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public VdfNode? this[string key] => Children.GetValueOrDefault(key);

    public string? GetString(string key) => Children.GetValueOrDefault(key)?.Value;
}

/// <summary>
/// Minimal recursive-descent parser for Valve's KeyValues text format. Handles quoted and
/// unquoted tokens, nested braces, and <c>//</c> line comments. Ignores conditionals/macros,
/// which the files we read (libraryfolders.vdf, *.acf, loginusers.vdf) don't rely on.
/// </summary>
public static class VdfParser
{
    public static VdfNode Parse(string text)
    {
        var pos = 0;
        var root = new VdfNode();
        ParseInto(root, text, ref pos);
        return root;
    }

    public static VdfNode ParseFile(string path) => Parse(File.ReadAllText(path));

    private static void ParseInto(VdfNode node, string s, ref int pos)
    {
        while (true)
        {
            var key = NextToken(s, ref pos);
            if (key is null) return;          // EOF
            if (key == "}") return;            // end of this object

            // Skip to the value/object after the key.
            SkipWhitespaceAndComments(s, ref pos);
            if (pos >= s.Length) return;

            if (s[pos] == '{')
            {
                pos++; // consume '{'
                var child = new VdfNode();
                ParseInto(child, s, ref pos);
                node.Children[key] = child;
            }
            else
            {
                var value = NextToken(s, ref pos);
                node.Children[key] = new VdfNode { Value = value ?? string.Empty };
            }
        }
    }

    /// <summary>Reads the next token: a quoted string, a bare word, or a lone brace.</summary>
    private static string? NextToken(string s, ref int pos)
    {
        SkipWhitespaceAndComments(s, ref pos);
        if (pos >= s.Length) return null;

        var c = s[pos];
        if (c == '{' || c == '}')
        {
            pos++;
            return c.ToString();
        }

        if (c == '"')
        {
            pos++; // opening quote
            var sb = new StringBuilder();
            while (pos < s.Length && s[pos] != '"')
            {
                if (s[pos] == '\\' && pos + 1 < s.Length)
                {
                    pos++;
                    sb.Append(s[pos] switch { 'n' => '\n', 't' => '\t', _ => s[pos] });
                }
                else
                {
                    sb.Append(s[pos]);
                }
                pos++;
            }
            pos++; // closing quote
            return sb.ToString();
        }

        // Bare token up to whitespace or a brace.
        var start = pos;
        while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '{' && s[pos] != '}')
            pos++;
        return s[start..pos];
    }

    private static void SkipWhitespaceAndComments(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            if (char.IsWhiteSpace(s[pos]))
            {
                pos++;
            }
            else if (s[pos] == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
            {
                while (pos < s.Length && s[pos] != '\n') pos++;
            }
            else
            {
                break;
            }
        }
    }
}
