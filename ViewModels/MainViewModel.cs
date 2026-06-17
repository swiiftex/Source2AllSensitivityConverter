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
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsBusy);
        DetectCommand = new RelayCommand(DetectSourceSensitivity, () => !IsBusy && Games.Count > 0);
        ApplyCommand = new RelayCommand(ApplyToSelected, () => !IsBusy && HasSelection);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true), () => !IsBusy);
        SelectNoneCommand = new RelayCommand(() => SetAllSelected(false), () => !IsBusy);
    }

    // ---- source sensitivity input ----

    private string _sourceSensitivityText = "1.0";
    public string SourceSensitivityText
    {
        get => _sourceSensitivityText;
        set
        {
            if (SetField(ref _sourceSensitivityText, value))
            {
                RecomputeAll();
                OnPropertyChanged(nameof(IsSourceValid));
            }
        }
    }

    public bool IsSourceValid => TryParseSource(out _);

    public double SourceYawDisplay => SensitivityConverter.SourceYaw;

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
            if (row.Game.Definition?.Applier is { } applier &&
                applier.TryReadCurrent(row.Game.InstallPath) is { } sens)
            {
                SourceSensitivityText = sens.ToString("0.######", CultureInfo.InvariantCulture);
                StatusMessage = $"Detected sensitivity {SourceSensitivityText} from {row.DisplayName}.";
                return;
            }
        }
        StatusMessage = "Could not read a sensitivity from any installed Source game's config.";
    }

    private void ApplyToSelected()
    {
        if (!TryParseSource(out var source))
        {
            StatusMessage = "Enter a valid source sensitivity first.";
            return;
        }

        int ok = 0, fail = 0;
        var problems = new List<string>();
        var exported = new List<(string Name, string Engine, double Value)>();

        foreach (var row in Games.Where(g => g is { IsSelected: true, IsEnabled: true }))
        {
            var def = row.Game.Definition!;
            var value = SensitivityConverter.ConvertFromSource(source, def);
            if (value is null) continue;

            if (def.Applier is { } applier)
            {
                var result = applier.Apply(row.Game.InstallPath, value.Value);
                if (result.Success) ok++;
                else { fail++; problems.Add($"{row.DisplayName}: {result.Message}"); }
            }
            else
            {
                // Experimental, no safe writer: export rather than risk corrupting the config.
                exported.Add((row.DisplayName, row.EngineText, value.Value));
            }
        }

        var parts = new List<string>();
        if (ok > 0) parts.Add($"applied to {ok} game(s)");
        if (exported.Count > 0) parts.Add(WriteExport(exported, source));
        if (fail > 0) parts.Add($"{fail} failed: {string.Join(" | ", problems)}");

        StatusMessage = parts.Count == 0
            ? "Nothing selected to apply."
            : string.Join("; ", parts) + ".";
    }

    /// <summary>
    /// Writes convert-only results to a text file on the Desktop and copies the values to the
    /// clipboard, so the user can paste them into games we can't safely auto-write.
    /// </summary>
    private static string WriteExport(List<(string Name, string Engine, double Value)> rows, double source)
    {
        var lines = new List<string>
        {
            "Source → All Sensitivity Converter — manual values",
            $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}  |  source sensitivity = " +
                source.ToString("0.######", CultureInfo.InvariantCulture),
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

    private void RecomputeAll()
    {
        if (!TryParseSource(out var source)) return;
        foreach (var row in Games) row.Recompute(source);
        OnPropertyChanged(nameof(SelectedDetails));
    }

    private bool TryParseSource(out double value)
        => double.TryParse(_sourceSensitivityText, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
           || double.TryParse(_sourceSensitivityText, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

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
