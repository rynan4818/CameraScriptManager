using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CameraScriptManager.Converters;

public class ModifiedRowBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush ModifiedBrush = new(Color.FromArgb(0xC0, 0xFF, 0xFF, 0x80));
    private static readonly SolidColorBrush NormalBrush = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isModified && isModified)
            return ModifiedBrush;
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }
}
