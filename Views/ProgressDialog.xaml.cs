using System;
using System.Threading.Tasks;
using System.Windows;

namespace CameraScriptManager.Views;

public partial class ProgressDialog : Window
{
    private readonly Func<Task> _workAsync;

    public ProgressDialog(string message, Func<Task> workAsync)
    {
        InitializeComponent();
        MessageText.Text = message;
        _workAsync = workAsync;
        
        Loaded += ProgressDialog_Loaded;
    }

    private async void ProgressDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _workAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"エラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Close();
        }
    }
}
