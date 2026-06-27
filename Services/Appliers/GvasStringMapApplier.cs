using System.Globalization;
using System.IO;
using System.Text;

namespace Source2AllSensitivityConverter.Services.Appliers;

/// <summary>
/// Writes a sensitivity into an Unreal Engine GVAS save (<c>.sav</c>) whose settings are stored as a
/// single <c>Map&lt;Name,Str&gt;</c> of option key → text value (e.g. THE FINALS'
/// <c>EmbarkOptionSaveGame.sav</c>, where <c>GameplayOption.Controls.MouseSensitivity</c> maps to a
/// string like <c>"11.0"</c>).
///
/// UE FStrings are <c>int32 length</c> (incl. the null terminator) followed by ASCII + <c>\0</c>.
/// Because the value is a length-prefixed string, changing it shifts the rest of the map, so the
/// <c>MapProperty</c>'s int64 payload-size field is adjusted by the same byte delta. The file is only
/// edited if it exists (saves are created by the game) and is backed up first.
/// </summary>
public sealed class GvasStringMapApplier(Func<string, string?> pathResolver, string optionKey) : IConfigApplier
{
    private static readonly byte[] MapPropertyMarker = Encoding.ASCII.GetBytes("MapProperty\0");

    public string TargetDescription => $"{ResolveForDisplay()}  (GVAS map: {optionKey})";

    private string ResolveForDisplay()
    {
        try { return pathResolver("") ?? "<save>"; } catch { return "<save>"; }
    }

    public ApplyResult Apply(string installPath, double sensitivity)
    {
        try
        {
            var path = pathResolver(installPath);
            if (string.IsNullOrEmpty(path))
                return ApplyResult.Fail("Save location could not be resolved.");
            if (!File.Exists(path))
                return ApplyResult.Fail($"Save not found (launch the game once first): {path}");

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 4 || Encoding.ASCII.GetString(bytes, 0, 4) != "GVAS")
                return ApplyResult.Fail("Not a GVAS save file.");

            if (!TryLocateValue(bytes, out var valueChunkStart, out var oldValueLen))
                return ApplyResult.Fail($"Option \"{optionKey}\" not found in the save.");

            var mapSizeOffset = FindMapSizeOffset(bytes);
            if (mapSizeOffset < 0)
                return ApplyResult.Fail("MapProperty size field not found.");

            var newValue = sensitivity.ToString("0.######", CultureInfo.InvariantCulture);
            var newStr = Encoding.ASCII.GetBytes(newValue);
            var newValueLen = newStr.Length + 1;                 // include null terminator
            var delta = newValueLen - oldValueLen;               // bytes added/removed
            var chunkEnd = valueChunkStart + 4 + oldValueLen;    // end of the old value chunk

            using var ms = new MemoryStream(bytes.Length + delta);
            ms.Write(bytes, 0, valueChunkStart);
            ms.Write(BitConverter.GetBytes(newValueLen), 0, 4);
            ms.Write(newStr, 0, newStr.Length);
            ms.WriteByte(0);
            ms.Write(bytes, chunkEnd, bytes.Length - chunkEnd);
            var result = ms.ToArray();

            // Keep the MapProperty payload-size field consistent.
            var oldSize = BitConverter.ToInt64(result, mapSizeOffset);
            BitConverter.GetBytes(oldSize + delta).CopyTo(result, mapSizeOffset);

            try
            {
                var bak = path + ".s2a-backup";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            catch { /* best effort */ }

            File.WriteAllBytes(path, result);
            return ApplyResult.Ok($"Set {optionKey} = {newValue} in {path}");
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
            if (!TryLocateValue(bytes, out var valueChunkStart, out var valueLen)) return null;

            var str = Encoding.ASCII.GetString(bytes, valueChunkStart + 4, valueLen - 1);
            return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Finds the value FString that follows the option key. Outputs the offset of the value's int32
    /// length prefix and the value's length (incl. null terminator).
    /// </summary>
    private bool TryLocateValue(byte[] bytes, out int valueChunkStart, out int valueLen)
    {
        valueChunkStart = 0;
        valueLen = 0;

        // The key is stored as a length-prefixed FString: int32(len incl null) + ASCII + \0.
        var key = Encoding.ASCII.GetBytes(optionKey);
        var pattern = new byte[4 + key.Length + 1];
        BitConverter.GetBytes(key.Length + 1).CopyTo(pattern, 0);
        key.CopyTo(pattern, 4);
        pattern[^1] = 0;

        var keyIdx = IndexOf(bytes, pattern, 0);
        if (keyIdx < 0) return false;

        valueChunkStart = keyIdx + pattern.Length;       // value's int32 length prefix
        if (valueChunkStart + 4 > bytes.Length) return false;

        valueLen = BitConverter.ToInt32(bytes, valueChunkStart);
        if (valueLen <= 0 || valueChunkStart + 4 + valueLen > bytes.Length) return false;
        return true;
    }

    private static int FindMapSizeOffset(byte[] bytes)
    {
        var idx = IndexOf(bytes, MapPropertyMarker, 0);
        if (idx < 0) return -1;
        var sizeOffset = idx + MapPropertyMarker.Length; // int64 immediately after "MapProperty\0"
        return sizeOffset + 8 <= bytes.Length ? sizeOffset : -1;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
