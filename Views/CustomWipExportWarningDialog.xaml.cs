using System.Windows;

namespace CameraScriptManager.Views;

public partial class CustomWipExportWarningDialog : Window
{
    public CustomWipExportWarningDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Proceed_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
