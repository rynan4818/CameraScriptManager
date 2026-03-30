using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using CameraScriptManager.Models;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class CopierView : UserControl
{
    private CopierViewModel? _viewModel;

    // ドラッグコピー用の状態保持変数
    private bool _isDraggingFillHandle;
    private SongScriptEntryViewModel? _dragSourceEntry;
    private Border? _activeFillHandle;

    public CopierView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 旧ViewModelの購読解除
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = e.NewValue as CopierViewModel;

        // 新ViewModelの購読
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateColumnVisibility();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CopierViewModel.ShowMetadataColumns):
                UpdateColumnVisibility();
                break;
        }
    }

    private void UpdateColumnVisibility()
    {
        if (_viewModel == null) return;
        var vis = _viewModel.ShowMetadataColumns ? Visibility.Visible : Visibility.Collapsed;
        ColSongSubName.Visibility = vis;
        ColSongAuthorName.Visibility = vis;
        ColLevelAuthorName.Visibility = vis;
        ColBpm.Visibility = vis;
        ColDuration.Visibility = vis;
    }

    private void EntryDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = e.OriginalSource as DependencyObject;
        if (dependencyObject == null) return;

        var row = FindAncestor<DataGridRow>(dependencyObject);
        if (row != null)
        {
            if (!row.IsSelected)
            {
                EntryDataGrid.SelectedItem = row.Item;
            }
        }
    }

    private void EntryDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DataGridSelectionToggleHelper.HandlePreviewMouseLeftButtonDown(EntryDataGrid, e);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_viewModel != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            _viewModel.HandleDroppedFiles(files);
        }
        e.Handled = true;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // --- コンテキストメニュー ---
    // (CommandBindingへ移行したため削除済)

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
        var col = EntryDataGrid.CurrentColumn;
        if (col == null) return;

        var selectedItems = EntryDataGrid.SelectedItems
            .OfType<SongScriptEntryViewModel>()
            .ToList();

        if (selectedItems.Count == 0) return;

        string header = col.Header?.ToString() ?? "";

        foreach (var item in selectedItems)
        {
            switch (header)
            {
                case "ID":
                    item.IsHexIdLocked = isLocked;
                    break;
                case "hash":
                    item.IsHashLocked = isLocked;
                    break;
                case "songName":
                    item.IsSongNameLocked = isLocked;
                    break;
                case "cameraScriptAuthorName":
                    item.IsCameraScriptAuthorLocked = isLocked;
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
                case "Duration":
                    item.IsDurationLocked = isLocked;
                    break;
                case "AvatarHeight(cm)":
                    item.IsAvatarHeightLocked = isLocked;
                    break;
                case "Description":
                    item.IsDescriptionLocked = isLocked;
                    break;
            }
        }
    }

    // --- ドラッグ（フィルハンドル）コピーの実装 ---

    private void FillHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is SongScriptEntryViewModel entry)
        {
            _isDraggingFillHandle = true;
            _dragSourceEntry = entry;
            _activeFillHandle = border;
            border.CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDraggingFillHandle && _dragSourceEntry != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));
            if (hit != null)
            {
                var row = FindAncestor<DataGridRow>(hit.VisualHit);
                if (row != null && row.Item is SongScriptEntryViewModel targetEntry)
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

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
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
            current = VisualTreeHelper.GetParent(current);
        }
        while (current != null);
        return null;
    }
}

