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
    // (CommandBindingへ移行したため削除済)

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

