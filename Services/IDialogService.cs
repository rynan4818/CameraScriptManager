using System.Windows;

namespace CameraScriptManager.Services;

public interface IDialogService
{
    void ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage image);
    MessageBoxResult ShowMessageBoxWithResult(string message, string title, MessageBoxButton button, MessageBoxImage image);
    void ShowUpdateAvailableDialog(string currentVersion, string latestVersion, string releaseUrl);
    string? ShowSaveFileDialog(string defaultFileName, string filter, string title);
    string? ShowOpenFolderDialog(string title, string? initialDirectory = null);
    void ShowProgressDialog(string title, Func<Action<string, double?>, Task> action);
    bool ShowCreatePlaylistDialog(CameraScriptManager.ViewModels.CreatePlaylistViewModel viewModel);
}
