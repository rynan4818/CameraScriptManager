using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using CameraScriptManager.ViewModels;

namespace CameraScriptManager.Views;

public partial class SongScriptsManagerView : UserControl
{
    private bool _isDraggingFillHandle;
    private SongScriptsManagerItemViewModel? _dragSourceEntry;
    private Border? _activeFillHandle;
    private SongScriptsManagerViewModel? _viewModel;
    private SongScriptsManagerViewModel ViewModel => (SongScriptsManagerViewModel)DataContext;

    public SongScriptsManagerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = e.NewValue as SongScriptsManagerViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateColumnVisibility();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SongScriptsManagerViewModel.ShowMetadataColumns))
        {
            UpdateColumnVisibility();
        }
    }

    private void UpdateColumnVisibility()
    {
        if (_viewModel == null)
        {
            return;
        }

        var visibility = _viewModel.ShowMetadataColumns ? Visibility.Visible : Visibility.Collapsed;
        ColSongSubName.Visibility = visibility;
        ColSongAuthorName.Visibility = visibility;
        ColLevelAuthorName.Visibility = visibility;
        ColBpm.Visibility = visibility;
        ColDuration.Visibility = visibility;
    }

    private void SongScriptsDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = e.OriginalSource as DependencyObject;
        if (dependencyObject == null)
            return;

        var row = FindAncestor<DataGridRow>(dependencyObject);
        if (row != null && !row.IsSelected)
        {
            SongScriptsDataGrid.SelectedItem = row.Item;
        }
    }

    private void SongScriptsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DataGridSelectionToggleHelper.HandlePreviewMouseLeftButtonDown(SongScriptsDataGrid, e);
    }

    private void LockCell_Click(object sender, RoutedEventArgs e)
    {
        SetLockOnCurrentColumn(true);
    }

    private async void FetchBeatSaver_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = SongScriptsDataGrid.SelectedItems
            .OfType<SongScriptsManagerItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            await ViewModel.FetchBeatSaverMetadataAsync(item);
        }
    }

    private async void DownloadSelectedMissingBeatmaps_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = SongScriptsDataGrid.SelectedItems
            .OfType<SongScriptsManagerItemViewModel>()
            .ToList();

        await ViewModel.DownloadSelectedMissingBeatmapsAsync(selectedItems);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void UnlockCell_Click(object sender, RoutedEventArgs e)
    {
        SetLockOnCurrentColumn(false);
    }

    private void SetLockOnCurrentColumn(bool isLocked)
    {
        var column = SongScriptsDataGrid.CurrentColumn;
        if (column == null)
            return;

        var selectedItems = SongScriptsDataGrid.SelectedItems
            .OfType<SongScriptsManagerItemViewModel>()
            .ToList();

        if (selectedItems.Count == 0)
            return;

        string header = column.Header?.ToString() ?? "";
        foreach (var item in selectedItems)
        {
            switch (header)
            {
                case "ID":
                    item.IsMapIdLocked = isLocked;
                    break;
                case "hash":
                    item.IsHashLocked = isLocked;
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

    private void ToggleSaveSelection_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = SongScriptsDataGrid.SelectedItems
            .OfType<SongScriptsManagerItemViewModel>()
            .ToList();

        if (selectedItems.Count == 0)
            return;

        bool newState = !selectedItems.First().IsSaveChecked;
        foreach (var item in selectedItems)
        {
            item.IsSaveChecked = newState;
        }
    }

    private void OpenSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = SongScriptsDataGrid.SelectedItems
            .OfType<SongScriptsManagerItemViewModel>()
            .ToList();

        foreach (var item in selectedItems)
        {
            string? folderPath = Path.GetDirectoryName(item.SourceFilePath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
            }
        }
    }

    private void FillHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is SongScriptsManagerItemViewModel entry)
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
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(this, e.GetPosition(this));
            if (hit != null)
            {
                var row = FindAncestor<DataGridRow>(hit.VisualHit);
                if (row != null && row.Item is SongScriptsManagerItemViewModel targetEntry)
                {
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
                return ancestor;

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        while (current != null);

        return null;
    }
}
