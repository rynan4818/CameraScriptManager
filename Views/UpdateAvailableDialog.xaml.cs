using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace CameraScriptManager.Views;

public partial class UpdateAvailableDialog : Window
{
    private readonly string _releaseUrl;

    public UpdateAvailableDialog(string currentVersion, string latestVersion, string releaseUrl)
    {
        InitializeComponent();

        CurrentVersionText.Text = currentVersion;
        LatestVersionText.Text = latestVersion;
        _releaseUrl = releaseUrl;
        ReleaseLinkText.Text = releaseUrl;

        if (Uri.TryCreate(releaseUrl, UriKind.Absolute, out Uri? releaseUri))
        {
            ReleaseLink.NavigateUri = releaseUri;
        }
        else
        {
            OpenReleaseButton.IsEnabled = false;
        }
    }

    private void ReleaseLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenReleaseUrl();
        e.Handled = true;
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        OpenReleaseUrl();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenReleaseUrl()
    {
        if (string.IsNullOrWhiteSpace(_releaseUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
    }
}
