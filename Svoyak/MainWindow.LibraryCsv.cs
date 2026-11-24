using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;

namespace Svoyak;

public partial class MainWindow
{
    private const string CsvFilter = "Данные игр Svoyak (*.csv)|*.csv";

    private async void ExportLibrary(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт данных игр в CSV",
            Filter = CsvFilter,
            DefaultExt = ".csv",
            FileName = $"Svoyak-{DateTime.Today:yyyy-MM-dd}.csv"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        await RunLibraryTransfer(() => LibraryCsv.Export(dialog.FileName, repository, timerSettings), "Экспортировано");
    }

    private async void ImportLibrary(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Импорт данных игр из CSV", Filter = CsvFilter };
        if (dialog.ShowDialog(this) != true)
            return;

        if (await RunLibraryTransfer(() => LibraryCsv.Import(dialog.FileName, repository, timerSettings), "Импортировано"))
            OpenLibrary(sender, e);
    }

    private async Task<bool> RunLibraryTransfer(Func<int> operation, string action)
    {
        IsEnabled = false;
        Cursor = Cursors.Wait;
        try
        {
            int games = await Task.Run(operation);
            MessageBox.Show(this, $"{action} игр: {games}.", "Перенос игр",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (Exception exception)
        {
            ShowError(exception);
            return false;
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }
    }
}
