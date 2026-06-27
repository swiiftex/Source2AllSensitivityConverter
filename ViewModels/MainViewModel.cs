using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services;

namespace Source2AllSensitivityConverter.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly InstalledGameScanner _scanner = new();
    private readonly AppSettings _settings;

    public ObservableCollection<GameRowViewModel> Games { get; } = [];

    /// <summary>Convertible catalog games plus any the user added manually (the dropdown source).</summary>
    public ObservableCollection<SourceOption> SourceOptions { get; } = [];

    public RelayCommand ScanCommand { get; }
    public RelayCommand DetectCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand CopyConvertCommand { get; }
    public RelayCommand CopySelectedCommand { get; }

    public MainViewModel()
    {
        _settings = SettingsStore.Load();

        foreach (var g in GameCatalog.All.Where(g => g.CanConvert).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
            SourceOptions.Add(new SourceOption(g));

        // Restore manually-added games into the dropdown.
        foreach (var cg in _settings.CustomGames)
            EnsureSourceOption(cg.Name, cg.ToDefinition());

        // Convert tab: restore the most recently used game/sensitivity, with sensible defaults.
        _convertFrom = FindOption(_settings.FromGame) ?? FindOption("Counter-Strike 2") ?? SourceOptions[0];
        _convertTo = FindOption(_settings.ToGame) ?? FindOption("VALORANT") ?? SourceOptions[0];
        _convertSensitivityText = _settings.Sensitivity ?? "1.0";

        // Auto-apply tab shares the same starting point so both tabs feel consistent.
        _selectedSource = _convertFrom;
        _sourceSensitivityText = _convertSensitivityText;

        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsBusy);
        DetectCommand = new RelayCommand(DetectSourceSensitivity, () => !IsBusy && Games.Count > 0);
        ApplyCommand = new RelayCommand(ApplyToSelected, () => !IsBusy && HasSelection && IsInputValid);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true), () => !IsBusy);
        SelectNoneCommand = new RelayCommand(() => SetAllSelected(false), () => !IsBusy);
        CopyConvertCommand = new RelayCommand(CopyConvertOutput, () => ConvertOutput.Length > 0);
        CopySelectedCommand = new RelayCommand(CopySelectedOutput,
            () => !string.IsNullOrEmpty(SelectedRow?.OutputValue));

        // Show manually-added games immediately (before any scan).
        IntegrateCustomGames();

        RecomputeConvert();
        OnInputChanged();
    }

    private SourceOption? FindOption(string? name)
        => name is null ? null : SourceOptions.FirstOrDefault(o => o.Game.Name == name);

    // ---- input: either a game's sensitivity, or a cm/360 + DPI ----

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

    // ---- Convert tab: a simple "from game + sensitivity -> to game" calculator ----

    private SourceOption _convertFrom;
    public SourceOption ConvertFrom
    {
        get => _convertFrom;
        set { if (SetField(ref _convertFrom, value)) RecomputeConvert(); }
    }

    private SourceOption _convertTo;
    public SourceOption ConvertTo
    {
        get => _convertTo;
        set { if (SetField(ref _convertTo, value)) RecomputeConvert(); }
    }

    private string _convertSensitivityText;
    public string ConvertSensitivityText
    {
        get => _convertSensitivityText;
        set { if (SetField(ref _convertSensitivityText, value)) RecomputeConvert(); }
    }

    private string _convertOutput = "";
    public string ConvertOutput
    {
        get => _convertOutput;
        private set
        {
            if (SetField(ref _convertOutput, value))
            {
                OnPropertyChanged(nameof(HasConvertOutput));
                CopyConvertCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasConvertOutput => _convertOutput.Length > 0;

    private string _convertNote = "";
    public string ConvertNote
    {
        get => _convertNote;
        private set => SetField(ref _convertNote, value);
    }

    private void RecomputeConvert()
    {
        if (TryParse(_convertSensitivityText, out var sens) && sens > 0
            && _convertFrom is not null && _convertTo is not null)
        {
            var counts = SensitivityConverter.CountsPer360(sens, _convertFrom.Yaw);
            var result = SensitivityConverter.SensitivityFromCounts(counts, _convertTo.Yaw);
            ConvertOutput = result.ToString("0.######", CultureInfo.InvariantCulture);
            var approx = _convertTo.Game.ApproximateYaw ? "approx. " : "";
            ConvertNote = $"{approx}{counts:0} counts/360  ·  {SensitivityConverter.CmPer360(counts, 800):0.0} cm/360 @ 800 DPI";
        }
        else
        {
            ConvertOutput = "";
            ConvertNote = "Enter a valid sensitivity.";
        }

        _settings.FromGame = _convertFrom?.Game.Name;
        _settings.ToGame = _convertTo?.Game.Name;
        _settings.Sensitivity = _convertSensitivityText;
        SettingsStore.Save(_settings);
    }

    private void CopyConvertOutput()
    {
        if (_convertOutput.Length == 0) return;
        ConvertNote = ClipboardHelper.TrySetText(_convertOutput)
            ? $"Copied {_convertOutput} to clipboard."
            : "Could not access the clipboard — try again.";
    }

    private void CopySelectedOutput()
    {
        var value = SelectedRow?.OutputValue;
        if (string.IsNullOrEmpty(value)) return;
        StatusMessage = ClipboardHelper.TrySetText(value)
            ? $"Copied {value} to clipboard."
            : "Could not access the clipboard — try again.";
    }

    // ---- manually-added games ----

    /// <summary>True when the selected game has no auto-apply yet (so a config can be added for it).</summary>
    public bool CanAddConfigForSelected => SelectedRow is { } r && !r.Game.CanAutoApply;

    /// <summary>
    /// Persist a user-defined game and apply it to the list. If a game with the same name is already
    /// shown (e.g. an unsupported scanned game), its row is upgraded in place instead of duplicated.
    /// </summary>
    public void AddCustomGame(CustomGame game)
    {
        _settings.CustomGames.RemoveAll(c => NameEquals(c.Name, game.Name));
        _settings.CustomGames.Add(game);
        SettingsStore.Save(_settings);

        var def = game.ToDefinition();
        var existing = Games.FirstOrDefault(r => NameEquals(r.DisplayName, game.Name));
        if (existing is not null)
            ReplaceRow(existing, Merge(existing.Game, def));
        else
            AddGameRow(game.ToDetectedGame());

        EnsureSourceOption(game.Name, def);
        StatusMessage = $"Added \"{game.Name}\" — its conversion now shows in the list.";
    }

    /// <summary>Merge custom games into the current list: upgrade matching rows, add the rest.</summary>
    private void IntegrateCustomGames()
    {
        foreach (var cg in _settings.CustomGames)
        {
            var def = cg.ToDefinition();
            var existing = Games.FirstOrDefault(r => NameEquals(r.DisplayName, cg.Name));
            if (existing is not null)
                ReplaceRow(existing, Merge(existing.Game, def));
            else
                AddGameRow(cg.ToDetectedGame());
        }
    }

    /// <summary>Attach a custom definition to an existing detected game, keeping its store/engine/path.</summary>
    private static DetectedGame Merge(DetectedGame existing, GameDefinition def) => new()
    {
        DisplayName = existing.DisplayName,
        InstallPath = existing.InstallPath,
        Store = existing.Store,
        Definition = def,
        DetectedEngine = existing.DetectedEngine != Engine.Unknown ? existing.DetectedEngine : Engine.Other,
    };

    private void ReplaceRow(GameRowViewModel oldRow, DetectedGame replacement)
    {
        var index = Games.IndexOf(oldRow);
        if (index < 0) { AddGameRow(replacement); return; }

        var wasSelected = ReferenceEquals(SelectedRow, oldRow);
        var row = MakeRow(replacement);
        Games[index] = row;
        if (wasSelected) SelectedRow = row;
    }

    private void EnsureSourceOption(string name, GameDefinition def)
    {
        for (var i = SourceOptions.Count - 1; i >= 0; i--)
            if (NameEquals(SourceOptions[i].Game.Name, name))
                SourceOptions.RemoveAt(i);

        if (def.CanConvert) SourceOptions.Add(new SourceOption(def));
    }

    private static bool NameEquals(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

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

    private string _statusMessage = "Scanning for installed games…";
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
            {
                OnPropertyChanged(nameof(SelectedDetails));
                OnPropertyChanged(nameof(CanAddConfigForSelected));
                CopySelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedDetails => SelectedRow?.Details
        ?? "Select a game to see its detected engine and converted sensitivity.";

    private bool HasSelection => Games.Any(g => g is { IsSelected: true, IsEnabled: true });

    // ---- actions ----

    /// <summary>Create a list row for a detected game, wire selection updates, and compute its value.</summary>
    private GameRowViewModel MakeRow(DetectedGame game)
    {
        var row = new GameRowViewModel(game);
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameRowViewModel.IsSelected)) RaiseCommandStates();
        };
        row.Recompute(CurrentCountsPer360());
        return row;
    }

    private void AddGameRow(DetectedGame game) => Games.Add(MakeRow(game));

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning Steam, Epic and GOG libraries…";
        try
        {
            var found = await Task.Run(() => _scanner.Scan());

            Games.Clear();
            foreach (var g in found) AddGameRow(g);
            IntegrateCustomGames();   // upgrade matching rows / add manually-added games

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

        foreach (var row in Games.Where(g => g is { IsSelected: true, IsEnabled: true }))
        {
            var def = row.Game.Definition!;
            if (def.Applier is not { } applier || def.YawConstant is not { } yaw) continue;

            var value = SensitivityConverter.SensitivityFromCounts(counts, yaw);
            var result = applier.Apply(row.Game.InstallPath, value);
            if (result.Success) ok++;
            else { fail++; problems.Add($"{row.DisplayName}: {result.Message}"); }
        }

        StatusMessage = (ok, fail) switch
        {
            (0, 0) => "Nothing selected to apply.",
            (_, 0) => $"Applied to {ok} game(s) successfully.",
            _ => $"Applied to {ok}, failed {fail}: {string.Join(" | ", problems)}",
        };
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
            CopySelectedCommand.RaiseCanExecuteChanged();
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Raise();
        else dispatcher.Invoke(Raise);
    }
}
