using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter.ViewModels;

/// <summary>
/// One entry in the "input sensitivity is from…" dropdown. Wraps a convertible catalog game so
/// the user can express their starting sensitivity in any game's units, not just Source.
/// </summary>
public sealed class SourceOption(GameDefinition game)
{
    public GameDefinition Game { get; } = game;

    public double Yaw => Game.YawConstant!.Value;

    /// <summary>Shown in the combo box: game name with its engine, e.g. "VALORANT  (Riot)".</summary>
    public string Display => $"{Game.Name}  ({Game.Engine})";
}
