using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CameraScriptManager.Views;

internal static class DataGridSelectionToggleHelper
{
    public static void HandlePreviewMouseLeftButtonDown(DataGrid dataGrid, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInteractiveElement(source))
        {
            return;
        }

        DataGridRow? row = FindAncestor<DataGridRow>(source);
        if (row == null || !row.IsSelected || dataGrid.SelectedItems.Count != 1)
        {
            return;
        }

        if (!ReferenceEquals(dataGrid.SelectedItems[0], row.Item))
        {
            return;
        }

        dataGrid.UnselectAll();
        dataGrid.SelectedItem = null;
        dataGrid.CurrentCell = new DataGridCellInfo();
        dataGrid.Focus();
        e.Handled = true;
    }

    private static bool IsInteractiveElement(DependencyObject source)
    {
        return FindAncestor<TextBoxBase>(source) != null
            || FindAncestor<PasswordBox>(source) != null
            || FindAncestor<ComboBox>(source) != null
            || FindAncestor<ButtonBase>(source) != null
            || FindAncestor<ListBoxItem>(source) != null
            || FindAncestor<Hyperlink>(source) != null
            || FindAncestor<ScrollBar>(source) != null
            || HasCrossCursorAncestor(source);
    }

    private static bool HasCrossCursorAncestor(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current != null)
        {
            if (current is FrameworkElement element && element.Cursor == Cursors.Cross)
            {
                return true;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is Visual || current is Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        if (current is FrameworkContentElement frameworkContentElement)
        {
            return frameworkContentElement.Parent;
        }

        if (current is ContentElement contentElement)
        {
            return ContentOperations.GetParent(contentElement) ?? LogicalTreeHelper.GetParent(contentElement);
        }

        return LogicalTreeHelper.GetParent(current);
    }
}
