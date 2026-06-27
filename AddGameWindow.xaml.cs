using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Source2AllSensitivityConverter.Models;
using Source2AllSensitivityConverter.Services;
using Source2AllSensitivityConverter.Services.Gvas;

namespace Source2AllSensitivityConverter;

public partial class AddGameWindow : Window
{
    /// <summary>The game the user defined, or null if they cancelled.</summary>
    public CustomGame? Result { get; private set; }

    private string _folder = "";
    private string _file = "";
    private bool _isGvas;
    private GvasMapInfo? _gvas;

    /// <summary>The write targets confirmed via Detect (a config can have several sensitivities).</summary>
    private readonly List<SensitivityTarget> _targets = [];

    public AddGameWindow(string? prefillName = null, string? prefillFolder = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(prefillName))
        {
            TxtName.Text = prefillName;
            ChkAuto.IsChecked = true;          // adding a config => reveal the auto-apply panel
        }
        if (!string.IsNullOrWhiteSpace(prefillFolder)) _folder = prefillFolder!;
    }

    private void OnToggleAuto(object sender, RoutedEventArgs e)
        => PanelAuto.Visibility = ChkAuto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select the game's save or config file",
            Filter = "Config/save files (*.sav;*.cfg;*.ini;*.txt;*.local)|*.sav;*.cfg;*.ini;*.txt;*.local|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(_folder) && Directory.Exists(_folder)) dlg.InitialDirectory = _folder;
        if (dlg.ShowDialog(this) != true) return;

        ClearTargets();
        _folder = Path.GetDirectoryName(dlg.FileName) ?? "";
        _file = Path.GetFileName(dlg.FileName);
        TxtFile.Text = dlg.FileName;

        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            if (GvasMapReader.IsGvas(bytes))
            {
                _isGvas = true;
                _gvas = GvasMapReader.TryParse(bytes);
                if (_gvas is null)
                {
                    TxtEditor.Text = "(This .sav is GVAS but its layout isn't supported for auto-apply.)";
                    Error("Couldn't parse this save's settings map. You can still add it as convert-only.");
                    return;
                }
                TxtEditor.Text = string.Join("\r\n", _gvas.Entries.Select(en => $"{en.Key} = {en.Value}"));
            }
            else
            {
                _isGvas = false;
                _gvas = null;
                TxtEditor.Text = File.ReadAllText(dlg.FileName);
            }
            Error("");
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AutoFindSensitivity));
        }
        catch (Exception ex)
        {
            Error($"Could not read the file: {ex.Message}");
        }
    }

    /// <summary>Find "Sensitivity", select the value, scroll to it and pulse the selection.</summary>
    private void AutoFindSensitivity()
    {
        if (!SensitivityHighlight.TryFindSensitivitySpan(TxtEditor.Text, out var selStart, out var selLen))
            return;

        TxtEditor.IsInactiveSelectionHighlightEnabled = true;
        TxtEditor.Select(selStart, selLen);
        var lineIndex = TxtEditor.GetLineIndexFromCharacterIndex(selStart);
        if (lineIndex >= 0) TxtEditor.ScrollToLine(lineIndex);
        TxtEditor.Focus();

        var pulse = new DoubleAnimation
        {
            From = 1.0,
            To = 0.15,
            Duration = TimeSpan.FromMilliseconds(220),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3),
        };
        TxtEditor.BeginAnimation(TextBoxBase.SelectionOpacityProperty, pulse);
    }

    private void OnDetect(object sender, RoutedEventArgs e)
    {
        Error("");
        if (_isGvas) DetectGvas(); else DetectText();
    }

    private void DetectText()
    {
        if (!SensitivityHighlight.TryBuildTemplate(TxtEditor.Text, TxtEditor.SelectionStart,
                TxtEditor.SelectionLength, out var template, out var value))
        {
            Error("Highlight exactly the sensitivity number (e.g. the 11.0).");
            return;
        }

        if (!Confirm(value, SensitivityHighlight.Classify(value) ?? "number")) return;
        AddTarget(new SensitivityTarget { Mode = AutoApplyMode.TextTemplate, SensitivityLine = template });
    }

    private void DetectGvas()
    {
        if (_gvas is null) { Error("No parsed save loaded."); return; }

        var lineIndex = CountLines(TxtEditor.Text, TxtEditor.SelectionStart);
        if (lineIndex < 0 || lineIndex >= _gvas.Entries.Count)
        {
            Error("Click the line with the sensitivity value, then Detect.");
            return;
        }

        var entry = _gvas.Entries[lineIndex];
        if (!SensitivityHighlight.IsNumber(entry.Value))
        {
            Error($"\"{entry.Key}\" isn't a number — select the line with the sensitivity value.");
            return;
        }

        if (!Confirm(entry.Value, _gvas.ValueKind.Word())) return;
        AddTarget(new SensitivityTarget
        {
            Mode = _gvas.ValueKind == GvasValueKind.Str ? AutoApplyMode.GvasString : AutoApplyMode.GvasNumeric,
            OptionKey = entry.Key,
            GvasKind = _gvas.ValueKind,
        });
    }

    private void AddTarget(SensitivityTarget target)
    {
        var label = target.Describe();
        if (_targets.Any(t => t.Describe() == label))
        {
            LblDetect.Text = "Already added.";
            return;
        }
        _targets.Add(target);
        LblDetect.Text = "Added.";
        RefreshTargets();
    }

    private void OnClearTargets(object sender, RoutedEventArgs e)
    {
        ClearTargets();
        LblDetect.Text = "Cleared.";
    }

    private void ClearTargets()
    {
        _targets.Clear();
        RefreshTargets();
    }

    private void RefreshTargets()
        => LblTargets.Text = _targets.Count == 0
            ? ""
            : $"Will write {_targets.Count} value(s):\r\n"
              + string.Join("\r\n", _targets.Select(t => "  • " + t.Describe()));

    private bool Confirm(string value, string kindWord)
        => MessageBox.Show(this,
            $"The detected sensitivity is {value} ({kindWord} value) — is this correct?",
            "Confirm value", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (name.Length == 0) { Error("Enter a game name."); return; }

        if (!double.TryParse(TxtEquiv.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var equiv)
            || equiv <= 0)
        {
            Error("Enter the value that equals CS2 sens 1.0 (a positive number).");
            return;
        }

        var game = new CustomGame { Name = name, EquivalentOfCs1 = equiv };

        if (ChkAuto.IsChecked == true)
        {
            if (string.IsNullOrEmpty(_file)) { Error("Choose the save/config file, or turn off auto-apply."); return; }
            if (_targets.Count == 0) { Error("Highlight each sensitivity value and confirm it with Detect."); return; }

            game.ConfigFolder = _folder;
            game.ConfigFile = _file;
            game.Targets = [.. _targets];
        }

        Result = game;
        DialogResult = true;
    }

    /// <summary>Zero-based index of the line containing <paramref name="caret"/>.</summary>
    private static int CountLines(string text, int caret)
    {
        if (caret < 0) return -1;
        caret = Math.Min(caret, text.Length);
        var count = 0;
        for (var i = 0; i < caret; i++)
            if (text[i] == '\n') count++;
        return count;
    }

    private void Error(string message) => LblError.Text = message;
}
