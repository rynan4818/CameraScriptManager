using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CameraScriptManager.Converters;

public class NullableBoolToCacheColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Brushes.Green : Brushes.Red;
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
