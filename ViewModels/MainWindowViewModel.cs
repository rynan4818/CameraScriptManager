using CameraScriptManager.Services;

namespace CameraScriptManager.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppUpdateCheckService _appUpdateCheckService = new();
    private readonly IDialogService _dialogService = new DialogService();
    private bool _hasCheckedForUpdatesOnStartup;

    public ManagerViewModel ManagerViewModel { get; }
    public CopierViewModel CopierViewModel { get; }
    public SongScriptsManagerViewModel SongScriptsManagerViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    public MainWindowViewModel()
    {
        ManagerViewModel = new ManagerViewModel();
        CopierViewModel = new CopierViewModel();
        SongScriptsManagerViewModel = new SongScriptsManagerViewModel();
        SettingsViewModel = new SettingsViewModel();

        SettingsViewModel.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, System.EventArgs e)
    {
        ManagerViewModel.ReloadSettings();
        CopierViewModel.ReloadSettings();
        SongScriptsManagerViewModel.ReloadSettings();
    }

    public async Task CheckForUpdatesOnStartupAsync()
    {
        if (_hasCheckedForUpdatesOnStartup)
        {
            return;
        }

        _hasCheckedForUpdatesOnStartup = true;

        AppUpdateCheckResult result = await _appUpdateCheckService.CheckForUpdatesAsync();
        if (!result.IsUpdateAvailable)
        {
            return;
        }

        _dialogService.ShowMessageBox(
            $"新しい CameraScriptManager が公開されています。\n現在のバージョン: {result.CurrentVersion}\n最新バージョン: {result.LatestVersion}\n\n最新版リリース:\n{result.ReleaseUrl}",
            "アップデートがあります",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
