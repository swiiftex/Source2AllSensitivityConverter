using System.Globalization;
using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services;

namespace Source2AllSensitivityConverter.ViewModels;

/// <summary>One row in the games list: a detected game plus its live converted value and state.</summary>
public sealed class GameRowViewModel(DetectedGame game) : ObservableObject
{
    public DetectedGame Game { get; } = game;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>
    /// A row's checkbox is enabled only when it can be auto-applied. Convert-only games stay
    /// greyed for the checkbox, but their value is still shown in the copyable output box.
    /// </summary>
    public bool IsEnabled => Game.CanAutoApply;

    public string DisplayName => Game.DisplayName;

    public string StoreText => Game.Store.ToString();

    public string EngineText => EngineNames.Display(Game.DetectedEngine);

    private double? _convertedSensitivity;
    public double? ConvertedSensitivity
    {
        get => _convertedSensitivity;
        private set
        {
            if (SetField(ref _convertedSensitivity, value))
            {
                OnPropertyChanged(nameof(OutputValue));
                OnPropertyChanged(nameof(OutputText));
                OnPropertyChanged(nameof(Details));
            }
        }
    }

    /// <summary>
    /// The plain converted number for the copyable text box — no decorations, so it can be
    /// pasted straight into a game. Empty when there is no value to copy.
    /// </summary>
    public string OutputValue => Game.CanConvert && ConvertedSensitivity is { } v
        ? v.ToString("0.######", CultureInfo.InvariantCulture)
        : "";

    /// <summary>True when this game's converted value is an approximation (shown as a marker).</summary>
    public bool IsApproximate => Game.CanConvert && Game.Definition!.ApproximateYaw;

    /// <summary>Decorated value used in the details panel (may carry a ≈ for approximations).</summary>
    public string OutputText
    {
        get
        {
            if (!Game.CanConvert) return "n/a";
            if (ConvertedSensitivity is not { } v) return "—";
            var s = v.ToString("0.######", CultureInfo.InvariantCulture);
            return IsApproximate ? $"≈ {s}" : s;
        }
    }

    /// <summary>Status shown in the right-hand column.</summary>
    public string StatusText => Game switch
    {
        { CanAutoApply: true } => "Auto-apply",
        { CanConvert: true } => "Copy value",
        { Definition: not null } => "No conversion",
        _ => "Unrecognised",
    };

    /// <summary>Detail blurb shown when a (possibly greyed) row is clicked.</summary>
    public string Details
    {
        get
        {
            var lines = new List<string>
            {
                $"{DisplayName}",
                $"Store:  {StoreText}",
                $"Engine: {EngineText}",
                $"Path:   {Game.InstallPath}",
            };

            if (Game.CanConvert)
            {
                lines.Add($"Output sensitivity: {OutputText}");
                if (Game.Definition!.ApproximateYaw)
                    lines.Add("(Conversion factor for this game is a close approximation.)");
            }
            else if (Game.Definition is not null)
                lines.Add("No reliable sensitivity conversion is known for this game's input model.");
            else
                lines.Add("This game is not in the conversion catalog.");

            if (Game.CanAutoApply)
                lines.Add($"Auto-apply target: {Game.Definition!.Applier!.TargetDescription}");
            else if (Game.CanConvert)
                lines.Add("Auto-apply is not supported for this game — copy the value from the output "
                          + "box and set it in-game yourself.");

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Recompute this game's sensitivity from a target-independent counts/360 "feel".
    /// Null clears the value (invalid input or no conversion for this game).
    /// </summary>
    public void Recompute(double? countsPer360)
    {
        ConvertedSensitivity = countsPer360 is { } c && Game.Definition?.YawConstant is { } yaw
            ? SensitivityConverter.SensitivityFromCounts(c, yaw)
            : null;
    }
}
