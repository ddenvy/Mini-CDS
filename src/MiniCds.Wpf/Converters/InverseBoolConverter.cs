// c:\Develop\Mini-CDS\src\MiniCds.Wpf\Converters\InverseBoolConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace MiniCds.Wpf.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
