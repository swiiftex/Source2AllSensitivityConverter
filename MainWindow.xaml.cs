using System.Windows;
using Source2AllSensitivityConverter.Services.Updates;
using Source2AllSensitivityConverter.ViewModels;

namespace Source2AllSensitivityConverter;

public partial class MainWindow : Window
{
    private bool _installing;

    public MainWindow()
    {
        InitializeComponent();
        Updater.Changed += OnUpdaterChanged;
        Closed += (_, _) => Updater.Changed -= OnUpdaterChanged;
        ShowUpdateState();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Always scan for installed games on startup.
        if (DataContext is MainViewModel vm && vm.ScanCommand.CanExecute(null))
            vm.ScanCommand.Execute(null);

        // Release builds look for a newer GitHub release in the background; local builds only on request.
        if (!AppVersion.Current.IsDevelopmentBuild)
            await Updater.CheckAsync();
    }

    private void OnManuallyAddGame(object sender, RoutedEventArgs e)
    {
        var dlg = new AddGameWindow { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } game && DataContext is MainViewModel vm)
            vm.AddCustomGame(game);
    }

    private void OnAddConfigForSelected(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedRow is not { } row) return;

        var dlg = new AddGameWindow(row.DisplayName, row.Game.InstallPath) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } game)
            vm.AddCustomGame(game);
    }

    // ---- Updates ----

    private void OnUpdaterChanged() => Dispatcher.BeginInvoke(ShowUpdateState);

    private void ShowUpdateState()
    {
        var current = AppVersion.Current;
        VersionText.Text = current.IsDevelopmentBuild ? "Development build" : $"Version {current.Display}";
        if (_installing) return;

        var release = Updater.Available;
        CheckUpdatesButton.IsEnabled = !Updater.IsChecking;
        InstallUpdateButton.Visibility = release != null ? Visibility.Visible : Visibility.Collapsed;
        InstallUpdateButton.Content = release != null ? $"Update to {release.Version.Display}" : "";
        UpdateStatusText.Text =
            Updater.IsChecking ? "Checking for updates…"
            : release != null ? (Updater.CanInstall(release) ? "A new version is available." : "A new version is on the releases page.")
            : Updater.LastError != null ? $"Couldn't check for updates: {Updater.LastError}."
            : Updater.LastChecked != null ? "You're on the latest version."
            : "";
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e) => await Updater.CheckAsync();

    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is not { } release || _installing) return;

        if (!Updater.CanInstall(release))
        {
            // No Windows build in the release, or this folder isn't writable: send them to the page instead.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(release.PageUrl) { UseShellExecute = true });
            return;
        }

        if (MessageBox.Show(this,
                $"Download and install {release.Name}?\n\nThe app closes, updates itself and opens again. Your settings are kept.",
                "Update Sensitivity Converter", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        _installing = true;
        CheckUpdatesButton.IsEnabled = InstallUpdateButton.IsEnabled = false;
        try
        {
            var progress = new Progress<double>(p => UpdateStatusText.Text = $"Downloading {release.Version.Display}… {p:P0}");
            var startSwap = await Updater.PrepareInstallAsync(release, progress, CancellationToken.None);
            UpdateStatusText.Text = "Restarting to finish the update…";
            startSwap();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _installing = false;
            InstallUpdateButton.IsEnabled = true;
            ShowUpdateState();
            UpdateStatusText.Text = "The update didn't install: " + ex.Message;
        }
    }
}
