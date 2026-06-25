using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services;

namespace Source2AllSensitivityConverter.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly InstalledGameScanner _scanner = new();

    public ObservableCollection<GameRowViewModel> Games { get; } = [];

    public RelayCommand ScanCommand { get; }
    public RelayCommand DetectCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }

    public MainViewModel()
    {
        // Default the source dropdown to Counter-Strike 2 (the canonical Source reference).
        _selectedSource = SourceOptions.FirstOrDefault(o => o.Game.Name == "Counter-Strike 2")
                          ?? SourceOptions[0];

        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsBusy);
        DetectCommand = new RelayCommand(DetectSourceSensitivity, () => !IsBusy && Games.Count > 0);
        ApplyCommand = new RelayCommand(ApplyToSelected, () => !IsBusy && HasSelection && IsInputValid);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true), () => !IsBusy);
        SelectNoneCommand = new RelayCommand(() => SetAllSelected(false), () => !IsBusy);

        OnInputChanged();
    }

    // ---- input: either a game's sensitivity, or a cm/360 + DPI ----

    /// <summary>All convertible catalog games, offered as the "input is from this game" source.</summary>
    public IReadOnlyList<SourceOption> SourceOptions { get; } =
        GameCatalog.All.Where(g => g.CanConvert)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SourceOption(g))
            .ToList();

    private SourceOption _selectedSource;
    public SourceOption SelectedSource
    {
        get => _selectedSource;
        set { if (SetField(ref _selectedSource, value)) OnInputChanged(); }
    }

    private string _sourceSensitivityText = "1.0";
    public string SourceSensitivityText
    {
        get => _sourceSensitivityText;
        set { if (SetField(ref _sourceSensitivityText, value)) OnInputChanged(); }
    }

    private bool _inputByCm360;
    /// <summary>When true, the input is a cm/360 + DPI instead of a game sensitivity.</summary>
    public bool InputByCm360
    {
        get => _inputByCm360;
        set
        {
            if (!SetField(ref _inputByCm360, value)) return;
            OnPropertyChanged(nameof(InputBySensitivity));
            OnInputChanged();
        }
    }

    public bool InputBySensitivity
    {
        get => !_inputByCm360;
        set => InputByCm360 = !value;
    }

    private string _cm360Text = "30";
    public string Cm360Text
    {
        get => _cm360Text;
        set { if (SetField(ref _cm360Text, value)) OnInputChanged(); }
    }

    private string _dpiText = "800";
    public string DpiText
    {
        get => _dpiText;
        set { if (SetField(ref _dpiText, value)) OnInputChanged(); }
    }

    public bool IsInputValid => CurrentCountsPer360() is not null;

    private string _inputSummary = "";
    public string InputSummary
    {
        get => _inputSummary;
        private set => SetField(ref _inputSummary, value);
    }

    // ---- options ----

    private bool _allowExperimentalApply;
    /// <summary>
    /// Opt-in: lets the user select convert-only games too. Games with a safe writer still get
    /// their config edited; the rest export their value to a Desktop file + clipboard.
    /// </summary>
    public bool AllowExperimentalApply
    {
        get => _allowExperimentalApply;
        set
        {
            if (!SetField(ref _allowExperimentalApply, value)) return;
            foreach (var row in Games) row.AllowExperimental = value;
            RaiseCommandStates();
            OnPropertyChanged(nameof(SelectedDetails));
        }
    }

    // ---- state ----

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value)) RaiseCommandStates();
        }
    }

    private string _statusMessage = "Click \"Scan for games\" to begin.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private GameRowViewModel? _selectedRow;
    public GameRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetField(ref _selectedRow, value))
                OnPropertyChanged(nameof(SelectedDetails));
        }
    }

    public string SelectedDetails => SelectedRow?.Details
        ?? "Select a game to see its detected engine and converted sensitivity.";

    private bool HasSelection => Games.Any(g => g is { IsSelected: true, IsEnabled: true });

    // ---- actions ----

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning Steam, Epic and GOG libraries…";
        try
        {
            var found = await Task.Run(() => _scanner.Scan());

            Games.Clear();
            foreach (var g in found)
            {
                var row = new GameRowViewModel(g) { AllowExperimental = AllowExperimentalApply };
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(GameRowViewModel.IsSelected)) RaiseCommandStates();
                };
                Games.Add(row);
            }

            RecomputeAll();

            var stores = string.Join(", ", _scanner.AvailableStores);
            StatusMessage = found.Count == 0
                ? $"No games found. Stores detected: {(string.IsNullOrEmpty(stores) ? "none" : stores)}."
                : $"Found {found.Count} game(s) across: {stores}. "
                  + $"{found.Count(x => x.CanAutoApply)} can be auto-applied.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void DetectSourceSensitivity()
    {
        foreach (var row in Games)
        {
            if (row.Game.Definition is { CanConvert: true } def && def.Applier is { } applier &&
                applier.TryReadCurrent(row.Game.InstallPath) is { } sens)
            {
                // Switch to sensitivity input, point the source at this game, and fill its value.
                InputByCm360 = false;
                SelectedSource = SourceOptions.FirstOrDefault(o => o.Game == def) ?? SelectedSource;
                SourceSensitivityText = sens.ToString("0.######", CultureInfo.InvariantCulture);
                StatusMessage = $"Detected sensitivity {SourceSensitivityText} from {row.DisplayName}.";
                return;
            }
        }
        StatusMessage = "Could not read a sensitivity from any installed game's config.";
    }

    private void ApplyToSelected()
    {
        if (CurrentCountsPer360() is not { } counts)
        {
            StatusMessage = "Enter a valid sensitivity (or cm/360 + DPI) first.";
            return;
        }

        int ok = 0, fail = 0;
        var problems = new List<string>();
        var exported = new List<(string Name, string Engine, double Value)>();

        foreach (var row in Games.Where(g => g is { IsSelected: true, IsEnabled: true }))
        {
            var def = row.Game.Definition!;
            if (def.YawConstant is not { } yaw) continue;
            var value = SensitivityConverter.SensitivityFromCounts(counts, yaw);

            if (def.Applier is { } applier)
            {
                var result = applier.Apply(row.Game.InstallPath, value);
                if (result.Success) ok++;
                else { fail++; problems.Add($"{row.DisplayName}: {result.Message}"); }
            }
            else
            {
                // Experimental, no safe writer: export rather than risk corrupting the config.
                exported.Add((row.DisplayName, row.EngineText, value));
            }
        }

        var parts = new List<string>();
        if (ok > 0) parts.Add($"applied to {ok} game(s)");
        if (exported.Count > 0) parts.Add(WriteExport(exported, counts));
        if (fail > 0) parts.Add($"{fail} failed: {string.Join(" | ", problems)}");

        StatusMessage = parts.Count == 0
            ? "Nothing selected to apply."
            : string.Join("; ", parts) + ".";
    }

    /// <summary>
    /// Writes convert-only results to a text file on the Desktop and copies the values to the
    /// clipboard, so the user can paste them into games we can't safely auto-write.
    /// </summary>
    private string WriteExport(List<(string Name, string Engine, double Value)> rows, double counts)
    {
        var lines = new List<string>
        {
            "Source → All Sensitivity Converter — manual values",
            $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}  |  {InputSummary}",
            new string('-', 56),
        };
        lines.AddRange(rows.Select(r =>
            $"{r.Name,-32} [{r.Engine}]  ->  {r.Value.ToString("0.######", CultureInfo.InvariantCulture)}"));

        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Source2All_Sensitivities.txt");
            File.WriteAllLines(path, lines);

            try { System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, lines)); }
            catch { /* clipboard may be locked by another app */ }

            return $"exported {rows.Count} value(s) to {path} (and clipboard)";
        }
        catch (Exception ex)
        {
            return $"could not write export file: {ex.Message}";
        }
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var row in Games.Where(g => g.IsEnabled))
            row.IsSelected = selected;
        RaiseCommandStates();
    }

    /// <summary>Called whenever any input field changes: recompute everything and refresh UI state.</summary>
    private void OnInputChanged()
    {
        var counts = CurrentCountsPer360();
        foreach (var row in Games) row.Recompute(counts);

        InputSummary = BuildInputSummary(counts);
        OnPropertyChanged(nameof(IsInputValid));
        OnPropertyChanged(nameof(SelectedDetails));
        RaiseCommandStates();
    }

    private void RecomputeAll() => OnInputChanged();

    /// <summary>
    /// Reduce the current input (game sensitivity, or cm/360 + DPI) to a single counts/360 value.
    /// Returns null when the input is incomplete or invalid.
    /// </summary>
    private double? CurrentCountsPer360()
    {
        if (_inputByCm360)
        {
            if (TryParse(_cm360Text, out var cm) && cm > 0 &&
                TryParse(_dpiText, out var dpi) && dpi > 0)
                return SensitivityConverter.CountsPer360FromCm(cm, dpi);
            return null;
        }

        if (TryParse(_sourceSensitivityText, out var sens) && sens > 0 && _selectedSource is not null)
            return SensitivityConverter.CountsPer360(sens, _selectedSource.Yaw);
        return null;
    }

    private string BuildInputSummary(double? counts)
    {
        if (counts is not { } c) return "Enter a valid sensitivity, or a cm/360 + DPI.";
        var summary = $"≈ {c:0} counts/360";
        if (TryParse(_dpiText, out var dpi) && dpi > 0)
            summary += $"  ({SensitivityConverter.CmPer360(c, dpi):0.0} cm/360 @ {dpi:0} DPI)";
        return summary;
    }

    private static bool TryParse(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
           || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    private void RaiseCommandStates()
    {
        // Marshal to UI thread in case a background continuation triggers this.
        void Raise()
        {
            ScanCommand.RaiseCanExecuteChanged();
            DetectCommand.RaiseCanExecuteChanged();
            ApplyCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
            SelectNoneCommand.RaiseCanExecuteChanged();
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Raise();
        else dispatcher.Invoke(Raise);
    }
}
