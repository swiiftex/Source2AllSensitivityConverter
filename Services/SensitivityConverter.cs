using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Converts an in-game sensitivity value between games using the "360 distance" method:
/// the physical distance (cm) to turn 360° is held constant, which makes the muscle-memory
/// feel identical across titles at the same mouse DPI.
///
/// For a linear-yaw engine, counts-per-360 = 360 / (sens * yaw). Holding that constant:
///     targetSens = sourceSens * sourceYaw / targetYaw
/// </summary>
public static class SensitivityConverter
{
    /// <summary>Source engine's default m_yaw. The canonical reference point for this app.</summary>
    public const double SourceYaw = 0.022;

    /// <summary>
    /// Convert <paramref name="sourceSens"/> (expressed in the source game's units, whose yaw
    /// is <paramref name="sourceYaw"/>) into the units of a game with yaw <paramref name="targetYaw"/>.
    /// </summary>
    public static double Convert(double sourceSens, double sourceYaw, double targetYaw)
    {
        if (sourceYaw <= 0 || targetYaw <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetYaw), "Yaw constants must be positive.");

        return sourceSens * sourceYaw / targetYaw;
    }

    /// <summary>
    /// Convert a Source-engine sensitivity (yaw = 0.022) into a target game's units.
    /// Returns null when the target game has no known linear conversion.
    /// </summary>
    public static double? ConvertFromSource(double sourceSens, GameDefinition target)
        => target.YawConstant is double yaw and > 0 ? Convert(sourceSens, SourceYaw, yaw) : null;

    /// <summary>
    /// The number of mouse counts required to turn a full 360° at the given sensitivity/yaw.
    /// Useful as a DPI-independent way to express the resulting feel.
    /// </summary>
    public static double CountsPer360(double sens, double yaw) => 360.0 / (sens * yaw);

    /// <summary>cm/360 for a given counts-per-360 and mouse DPI (2.54 cm per inch).</summary>
    public static double CmPer360(double countsPer360, int dpi) => countsPer360 / dpi * 2.54;
}
