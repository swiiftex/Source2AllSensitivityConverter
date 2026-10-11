using System.IO;
using System.Text;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Reads and writes config text without disturbing the file's encoding. Files with a byte-order mark
/// keep that encoding and BOM; files without one are treated as Latin-1, which maps every byte to a
/// char one-to-one, so untouched bytes (including any non-ASCII text) are written back unchanged.
/// </summary>
public static class TextFile
{
    public static string Read(string path, out Encoding encoding)
    {
        var bytes = File.ReadAllBytes(path);
        encoding = DetectEncoding(bytes, out var bomLength);
        return encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
    }

    public static void Write(string path, string text, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(text);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        fs.Write(preamble, 0, preamble.Length);
        fs.Write(body, 0, body.Length);
    }

    private static Encoding DetectEncoding(byte[] b, out int bomLength)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
        {
            bomLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
        {
            bomLength = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        }
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
        {
            bomLength = 2;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        }
        bomLength = 0;
        return Encoding.Latin1;
    }
}
