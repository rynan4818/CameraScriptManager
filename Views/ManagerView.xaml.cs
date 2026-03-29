using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class ManagerView : UserControl
{
    private ManagerViewModel ViewModel => (ManagerViewModel)DataContext;

    public ManagerView()
    {
        InitializeComponent();
        DataContext = new ManagerViewModel();
    }

    private void ScriptDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = e.OriginalSource as DependencyObject;
        if (dependencyObject == null) return;

        var row = FindAncestor<DataGridRow>(dependencyObject);
        if (row != null)
        {
            // 右クリックされた行が現在選択されていない場合、その行だけを選択する
            // 既に選択されている複数行のうちの1つの場合は、選択状態を維持する
            if (!row.IsSelected)
            {
                ScriptDataGrid.SelectedItem = row.Item;
            }
        }
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

    private void LockCell_Click(object sender, RoutedEventArgs e)
    {
        SetLockOnCurrentColumn(true);
    }

    private void UnlockCell_Click(object sender, RoutedEventArgs e)
    {
        SetLockOnCurrentColumn(false);
    }

    private void SetLockOnCurrentColumn(bool isLocked)
    {
        var col = ScriptDataGrid.CurrentColumn;
        if (col == null) return;

        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        if (selectedItems.Count == 0) return;

        string header = col.Header?.ToString() ?? "";

        foreach (var item in selectedItems)
        {
            switch (header)
            {
                case "ID":
                    item.IsMapIdLocked = isLocked;
                    break;
                case "cameraScriptAuthorName":
                    item.IsCameraScriptAuthorLocked = isLocked;
                    break;
                case "songName":
                    item.IsSongNameLocked = isLocked;
                    break;
                case "songSubName":
                    item.IsSongSubNameLocked = isLocked;
                    break;
                case "songAuthorName":
                    item.IsSongAuthorNameLocked = isLocked;
                    break;
                case "levelAuthorName":
                    item.IsLevelAuthorNameLocked = isLocked;
                    break;
                case "BPM":
                    item.IsBpmLocked = isLocked;
                    break;
                case "AvatarHeight":
                    item.IsAvatarHeightLocked = isLocked;
                    break;
                case "Description":
                    item.IsDescriptionLocked = isLocked;
                    break;
            }
        }
    }

    private void ToggleSelection_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        if (selectedItems.Count == 0) return;

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

        if (selectedItems.Count == 0)
        {
            return;
        }

        if (!ViewModel.EnsureSelectedSourcePathsConfigured(selectedItems))
        {
            return;
        }

        foreach (var item in selectedItems)
        {
            if (!ViewModel.TryBuildMapFolderPath(item, out var folderPath))
            {
                return;
            }

            if (System.IO.Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folderPath));
            }
        }
    }

    private void CopyIds_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ScriptDataGrid.SelectedItems
            .OfType<CameraScriptItemViewModel>()
            .ToList();

        if (selectedItems.Count == 0) return;

        var ids = selectedItems
            .Where(i => !string.IsNullOrWhiteSpace(i.MapId))
            .Select(i => i.MapId)
            .Distinct();
            
        string text = string.Join("\r\n", ids);
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
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
            
            // If the path contains a supported archive extension, we navigate to the folder containing it
            string[] archiveExts = { ".zip", ".7z", ".rar", ".tar", ".gz" };
            foreach (var ext in archiveExts)
            {
                int idx = targetPath.IndexOf(ext, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    targetPath = targetPath.Substring(0, idx + ext.Length);
                    break;
                }
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
