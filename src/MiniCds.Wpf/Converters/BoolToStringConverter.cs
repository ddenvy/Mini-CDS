// c:\Develop\Mini-CDS\src\MiniCds.Wpf\Converters\BoolToStringConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace MiniCds.Wpf.Converters;

public sealed class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Acquiring" : "Idle";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
