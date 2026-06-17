namespace Source2AllSensitivityConverter.Models;

/// <summary>
/// A game that was actually found installed on this machine, optionally matched to a
/// <see cref="GameDefinition"/> in the catalog.
/// </summary>
public sealed class DetectedGame
{
    public required string DisplayName { get; init; }

    public required string InstallPath { get; init; }

    public required Store Store { get; init; }

    public int? SteamAppId { get; init; }

    /// <summary>The catalog entry this install matched, if any.</summary>
    public GameDefinition? Definition { get; set; }

    /// <summary>
    /// Engine as determined either from the catalog match or from on-disk file structure
    /// when the game is not in the catalog.
    /// </summary>
    public Engine DetectedEngine { get; set; } = Engine.Unknown;

    public bool CanConvert => Definition?.CanConvert == true;

    public bool CanAutoApply => Definition?.CanAutoApply == true;
}
