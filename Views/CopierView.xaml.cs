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
    private readonly CopierViewModel _viewModel;

    // ドラッグコピー用の状態保持変数
    private bool _isDraggingFillHandle;
    private SongScriptEntryViewModel? _dragSourceEntry;
    private Border? _activeFillHandle;

    public CopierView()
    {
        InitializeComponent();
        _viewModel = new CopierViewModel();
        DataContext = _viewModel;
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
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
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

    private IList<SongScriptEntryViewModel> GetSelectedEntries()
    {
        return EntryDataGrid.SelectedItems.Cast<SongScriptEntryViewModel>().ToList();
    }

    private void ContextMenu_DeleteSelected(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedEntries();
        foreach (var entry in selected)
            _viewModel.Entries.Remove(entry);
    }

    private void ContextMenu_CopyIds(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedEntries();
        if (selected.Count == 0) return;

        var ids = selected
            .Where(i => !string.IsNullOrWhiteSpace(i.HexId))
            .Select(i => i.HexId)
            .Distinct();
            
        string text = string.Join("\r\n", ids);
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }

    private void ContextMenu_RenameNone(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            entry.RenameChoice = RenameOption.無し;
    }

    private void ContextMenu_RenameSongScript(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            entry.RenameChoice = RenameOption.SongScript;
    }

    private void ContextMenu_RenameAuthorIdSongName(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            entry.RenameChoice = RenameOption.AuthorIdSongName;
    }

    private void ContextMenu_CL_On(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            if (entry.CanCopyToCustomLevels) entry.CopyToCustomLevels = true;
    }

    private void ContextMenu_CL_Off(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            entry.CopyToCustomLevels = false;
    }

    private void ContextMenu_WIP_On(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            if (entry.CanCopyToCustomWIPLevels) entry.CopyToCustomWIPLevels = true;
    }

    private void ContextMenu_WIP_Off(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
            entry.CopyToCustomWIPLevels = false;
    }

    private void ContextMenu_OpenExplorerCL(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
        {
            var folder = entry.SelectedCustomLevelsFolder;
            if (folder != null && Directory.Exists(folder.FullPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder.FullPath));
            }
        }
    }

    private void ContextMenu_OpenExplorerWIP(object sender, RoutedEventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
        {
            var folder = entry.SelectedCustomWIPLevelsFolder;
            if (folder != null && Directory.Exists(folder.FullPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder.FullPath));
            }
        }
    }

    private void ContextMenu_SongNameSource(object sender, RoutedEventArgs e)
    {
        SetSongNameOptionForSelected(SongNameOption.Source);
    }

    private void ContextMenu_SongNameBeatSaver(object sender, RoutedEventArgs e)
    {
        SetSongNameOptionForSelected(SongNameOption.BeatSaverSongName);
    }

    private void ContextMenu_SongNameBeatSaverAndAuthor(object sender, RoutedEventArgs e)
    {
        SetSongNameOptionForSelected(SongNameOption.BeatSaverSongNameAndAuthor);
    }

    private async void SetSongNameOptionForSelected(SongNameOption option)
    {
        var selectedEntries = GetSelectedEntries().ToList();
        foreach (var entry in selectedEntries)
        {
            entry.SongNameChoice = option;
            
            // APIが必要なオプションで、まだMetadataを取得していない場合は取得しに行く
            if ((option == SongNameOption.BeatSaverSongName || option == SongNameOption.BeatSaverSongNameAndAuthor) && 
                entry.Model.Metadata == null)
            {
                await _viewModel.FetchApiDataAsync(entry);
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
                    // ドラッグ元と異なる行の上を通過した場合、値をコピーする
                    if (targetEntry != _dragSourceEntry)
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

