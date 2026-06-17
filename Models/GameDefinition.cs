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

    /// <summary>Steam application id, when the game ships on Steam. Primary match key.</summary>
    public int? SteamAppId { get; init; }

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

    public bool CanConvert => YawConstant is > 0;

    public bool CanAutoApply => Applier is not null;
}
