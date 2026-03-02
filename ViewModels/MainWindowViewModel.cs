using CameraScriptManager.ViewModels;

namespace CameraScriptManager.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ManagerViewModel ManagerViewModel { get; }
    public CopierViewModel CopierViewModel { get; }

    public MainWindowViewModel()
    {
        ManagerViewModel = new ManagerViewModel();
        CopierViewModel = new CopierViewModel();
    }
}
