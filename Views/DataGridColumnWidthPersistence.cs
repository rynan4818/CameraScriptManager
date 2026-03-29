using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CameraScriptManager.Services;

namespace CameraScriptManager.Views;

public static class DataGridColumnWidthPersistence
{
    public static readonly DependencyProperty EnablePersistenceProperty =
        DependencyProperty.RegisterAttached(
            "EnablePersistence",
            typeof(bool),
            typeof(DataGridColumnWidthPersistence),
            new PropertyMetadata(false, OnEnablePersistenceChanged));

    public static readonly DependencyProperty GridKeyProperty =
        DependencyProperty.RegisterAttached(
            "GridKey",
            typeof(string),
            typeof(DataGridColumnWidthPersistence),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ColumnKeyProperty =
        DependencyProperty.RegisterAttached(
            "ColumnKey",
            typeof(string),
            typeof(DataGridColumnWidthPersistence),
            new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty PersistenceStateProperty =
        DependencyProperty.RegisterAttached(
            "PersistenceState",
            typeof(PersistenceState),
            typeof(DataGridColumnWidthPersistence),
            new PropertyMetadata(null));

    public static bool GetEnablePersistence(DependencyObject obj) => (bool)obj.GetValue(EnablePersistenceProperty);
    public static void SetEnablePersistence(DependencyObject obj, bool value) => obj.SetValue(EnablePersistenceProperty, value);

    public static string GetGridKey(DependencyObject obj) => (string)obj.GetValue(GridKeyProperty);
    public static void SetGridKey(DependencyObject obj, string value) => obj.SetValue(GridKeyProperty, value);

    public static string GetColumnKey(DependencyObject obj) => (string)obj.GetValue(ColumnKeyProperty);
    public static void SetColumnKey(DependencyObject obj, string value) => obj.SetValue(ColumnKeyProperty, value);

    private static void OnEnablePersistenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            dataGrid.Loaded -= DataGrid_Loaded;
            dataGrid.Unloaded -= DataGrid_Unloaded;
            dataGrid.Loaded += DataGrid_Loaded;
            dataGrid.Unloaded += DataGrid_Unloaded;

            if (dataGrid.IsLoaded)
            {
                GetOrCreateState(dataGrid).Attach();
            }
        }
        else
        {
            dataGrid.Loaded -= DataGrid_Loaded;
            dataGrid.Unloaded -= DataGrid_Unloaded;
            GetState(dataGrid)?.Detach();
            SetState(dataGrid, null);
        }
    }

    private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            GetOrCreateState(dataGrid).Attach();
        }
    }

    private static void DataGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            GetState(dataGrid)?.Detach();
        }
    }

    private static PersistenceState GetOrCreateState(DataGrid dataGrid)
    {
        var state = GetState(dataGrid);
        if (state != null)
        {
            return state;
        }

        state = new PersistenceState(dataGrid);
        SetState(dataGrid, state);
        return state;
    }

    private static PersistenceState? GetState(DataGrid dataGrid)
    {
        return (PersistenceState?)dataGrid.GetValue(PersistenceStateProperty);
    }

    private static void SetState(DataGrid dataGrid, PersistenceState? state)
    {
        dataGrid.SetValue(PersistenceStateProperty, state);
    }

    private sealed class PersistenceState
    {
        private static readonly DependencyPropertyDescriptor? WidthPropertyDescriptor =
            DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));

        private readonly DataGrid _dataGrid;
        private readonly DispatcherTimer _saveTimer;
        private readonly Dictionary<DataGridColumn, DataGridLength> _originalWidths = new();
        private readonly Dictionary<DataGridColumn, EventHandler> _widthChangedHandlers = new();
        private bool _isAttached;
        private bool _isApplyingWidths;

        public PersistenceState(DataGrid dataGrid)
        {
            _dataGrid = dataGrid;
            _saveTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(350),
                DispatcherPriority.Background,
                SaveTimer_Tick,
                _dataGrid.Dispatcher);
            _saveTimer.Stop();
        }

        public void Attach()
        {
            if (_isAttached)
            {
                return;
            }

            CaptureOriginalWidths();
            AttachWidthHandlers();
            ApplySavedWidths();
            ColumnWidthSettingsService.ColumnWidthsReset += ColumnWidthSettingsService_ColumnWidthsReset;
            _isAttached = true;
        }

        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            _saveTimer.Stop();
            ColumnWidthSettingsService.ColumnWidthsReset -= ColumnWidthSettingsService_ColumnWidthsReset;

            if (WidthPropertyDescriptor != null)
            {
                foreach (var pair in _widthChangedHandlers)
                {
                    WidthPropertyDescriptor.RemoveValueChanged(pair.Key, pair.Value);
                }
            }

            _widthChangedHandlers.Clear();
            _isAttached = false;
        }

        private void CaptureOriginalWidths()
        {
            foreach (var column in _dataGrid.Columns)
            {
                if (!_originalWidths.ContainsKey(column))
                {
                    _originalWidths[column] = column.Width;
                }
            }
        }

        private void AttachWidthHandlers()
        {
            if (WidthPropertyDescriptor == null)
            {
                return;
            }

            foreach (var column in _dataGrid.Columns)
            {
                if (_widthChangedHandlers.ContainsKey(column))
                {
                    continue;
                }

                EventHandler handler = (_, _) => ScheduleSave();
                WidthPropertyDescriptor.AddValueChanged(column, handler);
                _widthChangedHandlers[column] = handler;
            }
        }

        private void ApplySavedWidths()
        {
            string gridKey = GetGridKey(_dataGrid);
            if (string.IsNullOrWhiteSpace(gridKey))
            {
                return;
            }

            var savedWidths = ColumnWidthSettingsService.LoadColumnWidths(gridKey);
            if (savedWidths.Count == 0)
            {
                return;
            }

            _isApplyingWidths = true;
            try
            {
                foreach (var column in _dataGrid.Columns)
                {
                    string columnKey = GetColumnKey(column);
                    if (string.IsNullOrWhiteSpace(columnKey) ||
                        !savedWidths.TryGetValue(columnKey, out double width) ||
                        width <= 0)
                    {
                        continue;
                    }

                    column.Width = new DataGridLength(width);
                }
            }
            finally
            {
                _isApplyingWidths = false;
            }
        }

        private void ScheduleSave()
        {
            if (_isApplyingWidths || !_isAttached)
            {
                return;
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveCurrentWidths();
        }

        private void SaveCurrentWidths()
        {
            if (_isApplyingWidths)
            {
                return;
            }

            string gridKey = GetGridKey(_dataGrid);
            if (string.IsNullOrWhiteSpace(gridKey))
            {
                return;
            }

            var widths = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in _dataGrid.Columns)
            {
                string columnKey = GetColumnKey(column);
                if (string.IsNullOrWhiteSpace(columnKey))
                {
                    continue;
                }

                double width = column.ActualWidth > 0
                    ? column.ActualWidth
                    : column.Width.DisplayValue;
                if (width > 0)
                {
                    widths[columnKey] = width;
                }
            }

            ColumnWidthSettingsService.SaveColumnWidths(gridKey, widths);
        }

        private void ColumnWidthSettingsService_ColumnWidthsReset(object? sender, EventArgs e)
        {
            if (!_dataGrid.Dispatcher.CheckAccess())
            {
                _dataGrid.Dispatcher.Invoke(() => ColumnWidthSettingsService_ColumnWidthsReset(sender, e));
                return;
            }

            _saveTimer.Stop();
            _isApplyingWidths = true;
            try
            {
                foreach (var pair in _originalWidths)
                {
                    pair.Key.Width = pair.Value;
                }
            }
            finally
            {
                _isApplyingWidths = false;
            }
        }
    }
}
