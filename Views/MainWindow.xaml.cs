using System;
using System.Windows;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class MainWindow : Window
{
    private bool _isSongScriptsInitialized;

    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += MainWindow_ContentRendered;
    }

    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        if (_isSongScriptsInitialized || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _isSongScriptsInitialized = true;
        await viewModel.SongScriptsManagerViewModel.InitializeAsync();
        await viewModel.CheckForUpdatesOnStartupAsync();
    }
}
