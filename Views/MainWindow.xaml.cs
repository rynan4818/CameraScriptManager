using System.Windows;
using System.Windows.Controls;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private async void FetchBeatSaver_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            await ViewModel.FetchBeatSaverMetadataAsync(item);
        }
    }

    private void LockAuthor_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            item.IsCameraScriptAuthorLocked = true;
        }
    }

    private void UnlockAuthor_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            item.IsCameraScriptAuthorLocked = false;
        }
    }

    private void ToggleSelection_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        if (selectedItems.Count == 0) return;

        // Toggle based on the first item's state to unify
        bool newState = !selectedItems.First().IsSelected;

        foreach (var item in selectedItems)
        {
            item.IsSelected = newState;
        }
    }

    private void OpenMapFolder_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            var folderPath = System.IO.Path.Combine(
                item.SourceType == "CustomWIPLevels" ? ViewModel.CustomWIPLevelsPath : ViewModel.CustomLevelsPath,
                item.FolderName);

            if (System.IO.Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folderPath));
            }
        }
    }

    private void OpenOriginalFolder_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            string? targetPath = item.SelectedOriginalSourceFile ?? item.OriginalSourceFiles.FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(targetPath))
                continue;
            
            // If the path contains a .zip, we navigate to the folder containing the .zip
            int zipIdx = targetPath.IndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (zipIdx >= 0)
            {
                targetPath = targetPath.Substring(0, zipIdx + 4);
            }

            var folderPath = System.IO.Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(folderPath) && System.IO.Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folderPath));
            }
        }
    }

    // --- ドラッグ（フィルハンドル）コピーの実装 ---

    private bool _isDraggingFillHandle;
    private CameraScriptItemViewModel? _dragSourceEntry;
    private Border? _activeFillHandle;

    private void FillHandle_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is CameraScriptItemViewModel entry)
        {
            _isDraggingFillHandle = true;
            _dragSourceEntry = entry;
            _activeFillHandle = border;
            border.CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDraggingFillHandle && _dragSourceEntry != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(this, e.GetPosition(this));
            if (hit != null)
            {
                var row = FindAncestor<DataGridRow>(hit.VisualHit);
                if (row != null && row.Item is CameraScriptItemViewModel targetEntry)
                {
                    // ドラッグ元と異なる行の上を通過した場合、値をコピーする (ロックされていない場合のみ)
                    if (targetEntry != _dragSourceEntry && !targetEntry.IsCameraScriptAuthorLocked)
                    {
                        targetEntry.CameraScriptAuthorName = _dragSourceEntry.CameraScriptAuthorName;
                    }
                }
            }
        }
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isDraggingFillHandle)
        {
            _isDraggingFillHandle = false;
            _dragSourceEntry = null;
            if (_activeFillHandle != null)
            {
                _activeFillHandle.ReleaseMouseCapture();
                _activeFillHandle = null;
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        do
        {
            if (current is T ancestor)
            {
                return ancestor;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        while (current != null);
        return null;
    }
}
