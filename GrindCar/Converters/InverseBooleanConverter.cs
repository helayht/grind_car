using System;
using System.Globalization;
using System.Windows.Data;

namespace GrindCar.Converters;

/// <summary>
/// 反向布尔转换器（true->false, false->true）
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : false;
    }
}

