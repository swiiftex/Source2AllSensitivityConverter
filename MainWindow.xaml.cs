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
}
