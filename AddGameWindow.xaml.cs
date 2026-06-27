using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Source2AllSensitivityConverter.Models;

namespace Source2AllSensitivityConverter;

public partial class AddGameWindow : Window
{
    /// <summary>The game the user defined, or null if they cancelled.</summary>
    public CustomGame? Result { get; private set; }

    public AddGameWindow() => InitializeComponent();

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select the game's config file",
            Filter = "Config files (*.cfg;*.ini;*.txt;*.local)|*.cfg;*.ini;*.txt;*.local|All files (*.*)|*.*",
            CheckFileExists = false,
        };
        if (!string.IsNullOrWhiteSpace(TxtFolder.Text) && Directory.Exists(TxtFolder.Text))
            dlg.InitialDirectory = TxtFolder.Text;

        if (dlg.ShowDialog(this) == true)
        {
            TxtFolder.Text = Path.GetDirectoryName(dlg.FileName) ?? "";
            TxtFile.Text = Path.GetFileName(dlg.FileName);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (name.Length == 0)
        {
            Error("Enter a game name.");
            return;
        }

        if (!double.TryParse(TxtEquiv.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var equiv)
            || equiv <= 0)
        {
            Error("Enter the value that equals CS2 sens 1.0 (a positive number).");
            return;
        }

        var folder = TxtFolder.Text.Trim();
        var file = TxtFile.Text.Trim();
        var line = TxtLine.Text.Trim();

        // Auto-apply fields are all-or-nothing: either fully specified, or left blank (convert-only).
        var anyAutoApply = folder.Length > 0 || file.Length > 0 || line.Length > 0;
        if (anyAutoApply)
        {
            if (folder.Length == 0 || file.Length == 0 || line.Length == 0)
            {
                Error("For auto-apply, fill in the folder, file, and sensitivity line — or clear all three.");
                return;
            }
            if (!line.Contains("{value}", StringComparison.OrdinalIgnoreCase))
            {
                Error("The sensitivity line must contain {value} where the number goes.");
                return;
            }
        }

        Result = new CustomGame
        {
            Name = name,
            EquivalentOfCs1 = equiv,
            ConfigFolder = folder,
            ConfigFile = file,
            SensitivityLine = line,
        };
        DialogResult = true;
    }

    private void Error(string message) => LblError.Text = message;
}
