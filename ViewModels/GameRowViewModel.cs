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

    private bool _allowExperimental;
    /// <summary>
    /// When the user opts in, convert-only games (a known sensitivity but no safe config writer)
    /// also become selectable. Toggled globally from the main view model.
    /// </summary>
    public bool AllowExperimental
    {
        get => _allowExperimental;
        set
        {
            if (!SetField(ref _allowExperimental, value)) return;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(StatusText));
            // Don't leave a now-disabled row checked.
            if (!IsEnabled) IsSelected = false;
        }
    }

    /// <summary>
    /// A row is checkable when it can be auto-applied safely, or when the user has enabled
    /// experimental apply and the game at least has a known conversion. Others stay greyed out.
    /// </summary>
    public bool IsEnabled => Game.CanAutoApply || (_allowExperimental && Game.CanConvert);

    public string DisplayName => Game.DisplayName;

    public string StoreText => Game.Store.ToString();

    public string EngineText => Game.DetectedEngine.ToString();

    private double? _convertedSensitivity;
    public double? ConvertedSensitivity
    {
        get => _convertedSensitivity;
        private set
        {
            if (SetField(ref _convertedSensitivity, value))
            {
                OnPropertyChanged(nameof(OutputText));
                OnPropertyChanged(nameof(Details));
            }
        }
    }

    public string OutputText
    {
        get
        {
            if (!Game.CanConvert) return "n/a";
            if (ConvertedSensitivity is not { } v) return "—";
            var s = v.ToString("0.######", CultureInfo.InvariantCulture);
            return Game.Definition!.ApproximateYaw ? $"≈ {s}" : s;
        }
    }

    /// <summary>Status shown in the right-hand column.</summary>
    public string StatusText => Game switch
    {
        { CanAutoApply: true } => "Auto-apply",
        { CanConvert: true } when _allowExperimental => "Export (exp.)",
        { CanConvert: true } => "Manual only",
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
            else if (Game.CanConvert && _allowExperimental)
                lines.Add("Experimental: no safe config writer for this engine. Applying exports the "
                          + "value to a file on your Desktop (and the clipboard) to enter manually.");
            else if (Game.CanConvert)
                lines.Add("Auto-apply is not supported here — set the value above manually in-game, "
                          + "or enable \"Allow experimental auto-apply\".");

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
