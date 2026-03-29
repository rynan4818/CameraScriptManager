using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CameraScriptManager.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void ReleaseUrl_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri == null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
