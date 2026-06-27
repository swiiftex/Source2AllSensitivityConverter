using System.Globalization;
using System.Text;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services.Gvas;

public sealed record GvasMapEntry(string Key, string Value);

public sealed class GvasMapInfo
{
    public required GvasValueKind ValueKind { get; init; }
    public required IReadOnlyList<GvasMapEntry> Entries { get; init; }
}

/// <summary>
/// Parses the first <c>MapProperty</c> in a GVAS (<c>.sav</c>) file into key → value entries so the
/// "Manually add game" editor can show the settings and detect the value type. Supports maps whose
/// value type is StrProperty / IntProperty / FloatProperty / DoubleProperty (the key is read as an
/// FString, which covers NameProperty and StrProperty keys).
/// </summary>
public static class GvasMapReader
{
    public static bool IsGvas(byte[] bytes) =>
        bytes.Length >= 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "GVAS";

    public static GvasMapInfo? TryParse(byte[] bytes)
    {
        try
        {
            if (!IsGvas(bytes)) return null;

            var mapIdx = IndexOf(bytes, Encoding.ASCII.GetBytes("MapProperty\0"), 0);
            if (mapIdx < 0) return null;

            var pos = mapIdx + "MapProperty\0".Length;
            pos += 8; // skip the int64 payload-size field

            _ = ReadFString(bytes, ref pos);             // KeyType (NameProperty/StrProperty)
            var valueType = ReadFString(bytes, ref pos); // ValueType
            var kind = valueType switch
            {
                "StrProperty" => GvasValueKind.Str,
                "IntProperty" => GvasValueKind.Int,
                "FloatProperty" => GvasValueKind.Float,
                "DoubleProperty" => GvasValueKind.Double,
                _ => GvasValueKind.Unknown,
            };
            if (kind == GvasValueKind.Unknown) return null;

            pos += 1; // HasPropertyGuid byte

            var numToRemove = ReadInt32(bytes, ref pos);
            for (var i = 0; i < numToRemove; i++) ReadFString(bytes, ref pos); // keys to remove

            var count = ReadInt32(bytes, ref pos);
            if (count < 0 || count > 100_000) return null;

            var entries = new List<GvasMapEntry>(count);
            for (var i = 0; i < count; i++)
            {
                if (pos >= bytes.Length) break;
                var key = ReadFString(bytes, ref pos);
                var value = kind switch
                {
                    GvasValueKind.Str => ReadFString(bytes, ref pos),
                    GvasValueKind.Int => ReadInt32(bytes, ref pos).ToString(CultureInfo.InvariantCulture),
                    GvasValueKind.Float => ReadFloat(bytes, ref pos).ToString("0.######", CultureInfo.InvariantCulture),
                    GvasValueKind.Double => ReadDouble(bytes, ref pos).ToString("0.######", CultureInfo.InvariantCulture),
                    _ => "",
                };
                entries.Add(new GvasMapEntry(key, value));
            }

            return new GvasMapInfo { ValueKind = kind, Entries = entries };
        }
        catch { return null; }
    }

    private static string ReadFString(byte[] b, ref int pos)
    {
        var len = ReadInt32(b, ref pos);
        if (len == 0) return "";
        if (len > 0)
        {
            var s = Encoding.ASCII.GetString(b, pos, len - 1);
            pos += len;
            return s;
        }
        var chars = -len;                          // negative length => UTF-16
        var u = Encoding.Unicode.GetString(b, pos, (chars - 1) * 2);
        pos += chars * 2;
        return u;
    }

    private static int ReadInt32(byte[] b, ref int pos) { var v = BitConverter.ToInt32(b, pos); pos += 4; return v; }
    private static float ReadFloat(byte[] b, ref int pos) { var v = BitConverter.ToSingle(b, pos); pos += 4; return v; }
    private static double ReadDouble(byte[] b, ref int pos) { var v = BitConverter.ToDouble(b, pos); pos += 8; return v; }

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
