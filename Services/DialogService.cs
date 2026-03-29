using System.IO;
using System.Windows;
using Microsoft.Win32;
using CameraScriptManager.Views;

namespace CameraScriptManager.Services;

public class DialogService : IDialogService
{
    public void ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage image)
    {
        MessageBox.Show(message, title, button, image);
    }

    public MessageBoxResult ShowMessageBoxWithResult(string message, string title, MessageBoxButton button, MessageBoxImage image)
    {
        return MessageBox.Show(message, title, button, image);
    }

    public string? ShowSaveFileDialog(string defaultFileName, string filter, string title)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            Title = title
        };

        if (dialog.ShowDialog() == true)
        {
            return dialog.FileName;
        }

        return null;
    }

    public string? ShowOpenFolderDialog(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowProgressDialog(string title, Func<Action<string, double?>, Task> action)
    {
        var dialog = new ProgressDialog(title, action);

        if (Application.Current?.MainWindow is Window owner &&
            owner.IsLoaded &&
            owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    public bool ShowCreatePlaylistDialog(CameraScriptManager.ViewModels.CreatePlaylistViewModel viewModel)
    {
        var dialog = new CreatePlaylistDialog(viewModel);
        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        return dialog.ShowDialog() == true;
    }
}
