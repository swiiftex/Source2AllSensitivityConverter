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

    /// <summary>Shown in the combo box — just the (already descriptive) game name, no raw enum.</summary>
    public string Display => Game.Name;

    // Also drive the display via ToString so the combo box renders correctly even where the
    // templated selection box doesn't honour DisplayMemberPath.
    public override string ToString() => Display;
}
