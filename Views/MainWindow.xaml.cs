using System;
using System.Windows;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += MainWindow_ContentRendered;
    }

    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.CheckForUpdatesOnStartupAsync();
    }
}
