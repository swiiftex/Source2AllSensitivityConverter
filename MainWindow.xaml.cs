using System.Windows;
using Source2AllSensitivityConverter.ViewModels;

namespace Source2AllSensitivityConverter;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Always scan for installed games on startup.
        if (DataContext is MainViewModel vm && vm.ScanCommand.CanExecute(null))
            vm.ScanCommand.Execute(null);
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
}
