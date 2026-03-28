using CameraScriptManager.ViewModels;

namespace CameraScriptManager.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
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
}
