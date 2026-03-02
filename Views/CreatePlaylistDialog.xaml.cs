using System.Windows;
using Microsoft.Win32;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class CreatePlaylistDialog : Window
{
    public CreatePlaylistDialog(CreatePlaylistViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "画像ファイル|*.jpg;*.jpeg;*.png|すべてのファイル|*.*",
            Title = "カバー画像を選択"
        };

        if (dialog.ShowDialog() == true)
        {
            if (DataContext is CreatePlaylistViewModel vm)
            {
                vm.CoverImagePath = dialog.FileName;
            }
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CreatePlaylistViewModel vm && string.IsNullOrWhiteSpace(vm.Title))
        {
            MessageBox.Show("タイトルは必須です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
