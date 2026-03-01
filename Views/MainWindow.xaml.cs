using System.Windows;
using System.Windows.Controls;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private async void FetchBeatSaver_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            await ViewModel.FetchBeatSaverMetadataAsync(item);
        }
    }

    private void LockAuthor_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            item.IsCameraScriptAuthorLocked = true;
        }
    }

    private void UnlockAuthor_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            item.IsCameraScriptAuthorLocked = false;
        }
    }
}
