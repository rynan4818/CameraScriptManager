using System.Collections.Generic;

namespace CameraScriptManager.Services;

public static class ColumnWidthSettingsService
{
    private static readonly object SyncRoot = new();

    public static event EventHandler? ColumnWidthsReset;

    public static IReadOnlyDictionary<string, double> LoadColumnWidths(string gridKey)
    {
        if (string.IsNullOrWhiteSpace(gridKey))
        {
            return new Dictionary<string, double>();
        }

        lock (SyncRoot)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            settings.ColumnWidths ??= new Dictionary<string, Dictionary<string, double>>();

            if (!settings.ColumnWidths.TryGetValue(gridKey, out var widths) || widths == null)
            {
                return new Dictionary<string, double>();
            }

            return new Dictionary<string, double>(widths, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SaveColumnWidths(string gridKey, IDictionary<string, double> widths)
    {
        if (string.IsNullOrWhiteSpace(gridKey))
        {
            return;
        }

        lock (SyncRoot)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            settings.ColumnWidths ??= new Dictionary<string, Dictionary<string, double>>();

            if (widths.Count == 0)
            {
                settings.ColumnWidths.Remove(gridKey);
            }
            else
            {
                settings.ColumnWidths[gridKey] = widths
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase);
            }

            settingsService.Save(settings);
        }
    }

    public static void ResetAllColumnWidths()
    {
        lock (SyncRoot)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            settings.ColumnWidths = new Dictionary<string, Dictionary<string, double>>();
            settingsService.Save(settings);
        }

        ColumnWidthsReset?.Invoke(null, EventArgs.Empty);
    }
}
