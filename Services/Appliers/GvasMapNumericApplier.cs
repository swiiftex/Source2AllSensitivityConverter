using System.Globalization;
using System.IO;
using System.Text;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Writes a numeric value (Int / Float / Double) stored as a map entry inside a GVAS save. Numeric
/// map values are fixed-width, so the value bytes are overwritten in place — no size field changes.
/// The entry is located by its key FString; the value bytes follow immediately.
/// </summary>
public sealed class GvasMapNumericApplier(Func<string, string?> pathResolver, string key, GvasValueKind kind)
    : IConfigApplier
{
    private int Width => kind == GvasValueKind.Double ? 8 : 4;

    public string TargetDescription
    {
        get { try { return $"{pathResolver("")}  (GVAS {kind.Word()}: {key})"; } catch { return $"GVAS: {key}"; } }
    }

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path)) return ApplyResult.Fail("Save location could not be resolved.");
            if (!File.Exists(path)) return ApplyResult.Fail($"Save not found (launch the game once first): {path}");

            var bytes = File.ReadAllBytes(path);
            if (!TryLocateValue(bytes, out var valueOffset)) return ApplyResult.Fail($"Option \"{key}\" not found.");

            var newBytes = kind switch
            {
                GvasValueKind.Int => BitConverter.GetBytes((int)Math.Round(sensitivity)),
                GvasValueKind.Float => BitConverter.GetBytes((float)sensitivity),
                GvasValueKind.Double => BitConverter.GetBytes(sensitivity),
                _ => null,
            };
            if (newBytes is null) return ApplyResult.Fail("Unsupported numeric kind.");

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* best effort */ }

            Array.Copy(newBytes, 0, bytes, valueOffset, Width);
            File.WriteAllBytes(path, bytes);
            return ApplyResult.Ok($"Set {key} = {sensitivity.ToString("0.######", CultureInfo.InvariantCulture)} in {path}");
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail($"Could not write save: {ex.Message}");
        }
    }

    public double? TryReadCurrent(string installPath)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var bytes = File.ReadAllBytes(path);
            if (!TryLocateValue(bytes, out var valueOffset)) return null;

            return kind switch
            {
                GvasValueKind.Int => BitConverter.ToInt32(bytes, valueOffset),
                GvasValueKind.Float => BitConverter.ToSingle(bytes, valueOffset),
                GvasValueKind.Double => BitConverter.ToDouble(bytes, valueOffset),
                _ => null,
            };
        }
        catch { return null; }
    }

    private bool TryLocateValue(byte[] bytes, out int valueOffset)
    {
        valueOffset = 0;
        var keyBytes = Encoding.ASCII.GetBytes(key);
        var pattern = new byte[4 + keyBytes.Length + 1];
        BitConverter.GetBytes(keyBytes.Length + 1).CopyTo(pattern, 0);
        keyBytes.CopyTo(pattern, 4);
        pattern[^1] = 0;

        var idx = IndexOf(bytes, pattern, 0);
        if (idx < 0) return false;
        valueOffset = idx + pattern.Length;
        return valueOffset + Width <= bytes.Length;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }
}
