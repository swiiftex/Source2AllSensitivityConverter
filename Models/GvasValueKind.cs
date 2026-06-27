namespace Source2AllSensitivityConverter.Models;

/// <summary>How a value is stored inside a GVAS save's map (drives detection and write width).</summary>
public enum GvasValueKind
{
    Unknown,
    Str,     // length-prefixed FString (numbers stored as text, e.g. "11.0")
    Int,     // int32
    Float,   // 32-bit float
    Double,  // 64-bit double
}

public static class GvasValueKindExtensions
{
    public static string Word(this GvasValueKind kind) => kind switch
    {
        GvasValueKind.Str => "text",
        GvasValueKind.Int => "integer",
        GvasValueKind.Float => "float",
        GvasValueKind.Double => "double",
        _ => "unknown",
    };
}
