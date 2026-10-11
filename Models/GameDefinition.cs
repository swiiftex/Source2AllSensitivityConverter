using Source2AllSensitivityConverter.Services.Appliers;

namespace Source2AllSensitivityConverter.Models;

/// <summary>
/// A known game in our catalog. Holds everything needed to (a) recognise an install,
/// (b) convert a sensitivity into this game's units, and (c) optionally write it to disk.
/// </summary>
public sealed class GameDefinition
{
    public required string Name { get; init; }

    public required Engine Engine { get; init; }

    /// <summary>
    /// Degrees rotated per raw mouse count at in-game sensitivity 1.0 (the "yaw" constant).
    /// Source defaults to 0.022. Conversions keep cm/360 constant across games:
    ///   targetSens = sourceSens * sourceYaw / targetYaw
    /// A null value means the game's sensitivity model is not a simple linear yaw and we
    /// don't have a reliable conversion (the game will be listed but not convertible).
    /// </summary>
    public double? YawConstant { get; init; }

    /// <summary>
    /// True when <see cref="YawConstant"/> is a commonly-cited estimate rather than a verified
    /// value. Surfaced in the UI so the user knows the converted number is a close approximation.
    /// </summary>
    public bool ApproximateYaw { get; init; }

    /// <summary>Steam application id, when the game ships on Steam. Primary match key.</summary>
    public int? SteamAppId { get; init; }

    /// <summary>
    /// Other Steam app ids that install the same game (e.g. separate multiplayer / zombies apps that
    /// share the install folder). Matched the same way as <see cref="SteamAppId"/>.
    /// </summary>
    public int[] AdditionalSteamAppIds { get; init; } = [];

    /// <summary>
    /// Steam app id of a launcher install this game lives inside (e.g. Call of Duty HQ), which holds
    /// several games in one folder. Each hosted game gets its own row from that one install.
    /// </summary>
    public int? HostSteamAppId { get; init; }

    /// <summary>
    /// DLC app ids that install this game inside its host. When the host's manifest lists installed
    /// DLC, only hosted games with a matching id are shown; with no DLC info every hosted game is.
    /// </summary>
    public int[] HostDlcAppIds { get; init; } = [];

    /// <summary>Lower-case fragments of the install folder name used as a fallback match.</summary>
    public string[] InstallDirHints { get; init; } = [];

    /// <summary>
    /// Relative paths (from the install root) that, if present, confirm this engine/game.
    /// Used to validate matches and to drive generic engine detection.
    /// </summary>
    public string[] MarkerFiles { get; init; } = [];

    /// <summary>
    /// Knows how to write the converted sensitivity into the game's config.
    /// Null means we cannot auto-apply (the game is greyed out in the UI).
    /// </summary>
    public IConfigApplier? Applier { get; init; }

    /// <summary>
    /// Base-turn term for games whose turn rate isn't proportional to the stored value. The model is
    /// <c>degrees per count = YawConstant × (value + SensitivityOffset)</c>; 0 for ordinary linear
    /// games. Frostbite (Battlefield) uses this — its config value has a fixed base turn added.
    /// </summary>
    public double SensitivityOffset { get; init; }

    /// <summary>
    /// For games whose turn rate isn't linear in the setting (exponential or piecewise sliders):
    /// degrees per count for a given setting. Must be paired with <see cref="SettingForDegrees"/>,
    /// and takes the place of <see cref="YawConstant"/>.
    /// </summary>
    public Func<double, double>? DegreesPerCount { get; init; }

    /// <summary>Inverse of <see cref="DegreesPerCount"/>: the setting giving a turn rate.</summary>
    public Func<double, double>? SettingForDegrees { get; init; }

    public bool CanConvert => YawConstant is > 0 || (DegreesPerCount is not null && SettingForDegrees is not null);

    /// <summary>Mouse counts for a full 360° turn at the given in-config sensitivity.</summary>
    public double CountsPer360(double sensitivity)
        => DegreesPerCount is { } f
            ? 360.0 / f(sensitivity)
            : 360.0 / (YawConstant!.Value * (sensitivity + SensitivityOffset));

    /// <summary>The in-config sensitivity that produces the given counts per 360°.</summary>
    public double SensitivityForCounts(double countsPer360)
        => SettingForDegrees is { } g
            ? g(360.0 / countsPer360)
            : 360.0 / (countsPer360 * YawConstant!.Value) - SensitivityOffset;

    public bool CanAutoApply => Applier is not null;
}
