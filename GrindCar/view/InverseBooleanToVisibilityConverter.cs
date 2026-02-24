using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GrindCar.view;

/// <summary>
/// 反向布尔到可见性转换器（true->Collapsed, false->Visible）
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool flag)
            return flag ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility != Visibility.Visible;
        return true;
    }
}
