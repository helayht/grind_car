using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GrindCar.Converters;

/// <summary>
/// 反向布尔到可见性转换器（true->Collapsed, false->Visible）
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 将布尔值反向转换为可见性状态。
    /// </summary>
    /// <param name="value">待转换的布尔值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">可选转换参数。</param>
    /// <param name="culture">当前区域信息。</param>
    /// <returns>输入为 <c>true</c> 时返回 <see cref="Visibility.Collapsed"/>，否则返回 <see cref="Visibility.Visible"/>。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool flag)
            return flag ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Collapsed;
    }

    /// <summary>
    /// 将可见性值反向转换为布尔值。
    /// </summary>
    /// <param name="value">待转换的可见性值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">可选转换参数。</param>
    /// <param name="culture">当前区域信息。</param>
    /// <returns>输入为非 <see cref="Visibility.Visible"/> 时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility != Visibility.Visible;
        return true;
    }
}

