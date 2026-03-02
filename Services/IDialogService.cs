using System.Windows;

namespace CameraScriptManager.Services;

public interface IDialogService
{
    void ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage image);
    MessageBoxResult ShowMessageBoxWithResult(string message, string title, MessageBoxButton button, MessageBoxImage image);
    string? ShowSaveFileDialog(string defaultFileName, string filter, string title);
    void ShowProgressDialog(string title, Func<Action<string, double?>, Task> action);
}
