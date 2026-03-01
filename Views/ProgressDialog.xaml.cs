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

    public ProgressDialog(string message, Func<Action<string, double?>, Task> workAsyncWithProgress)
    {
        InitializeComponent();
        MessageText.Text = message;
        _workAsync = () => workAsyncWithProgress(UpdateProgress);

        Loaded += ProgressDialog_Loaded;
    }

    public void UpdateProgress(string message, double? percent)
    {
        Dispatcher.Invoke(() =>
        {
            MessageText.Text = message;
            if (percent.HasValue)
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = percent.Value;
            }
            else
            {
                ProgressBar.IsIndeterminate = true;
            }
        });
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
