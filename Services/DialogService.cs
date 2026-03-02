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

    public void ShowProgressDialog(string title, Func<Action<string, double?>, Task> action)
    {
        var dialog = new ProgressDialog(title, action);

        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.ShowDialog();
    }
}
