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
        SettingsViewModel.RefreshAppUpdateInfo();

        if (!result.WasCheckedOnline || !result.IsUpdateAvailable)
        {
            return;
        }

        _dialogService.ShowUpdateAvailableDialog(result.CurrentVersion, result.LatestVersion, result.ReleaseUrl);
    }
}
