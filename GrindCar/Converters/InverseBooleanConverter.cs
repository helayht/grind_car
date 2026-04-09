using System;
using System.Globalization;
using System.Windows.Data;

namespace GrindCar.Converters;

/// <summary>
/// 反向布尔转换器（true->false, false->true）
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// 将布尔值反转后返回，供界面绑定时使用。
    /// </summary>
    /// <param name="value">待转换的原始值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">可选转换参数。</param>
    /// <param name="culture">当前区域信息。</param>
    /// <returns>反转后的布尔值；若输入无效则返回 <c>true</c>。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : true;
    }

    /// <summary>
    /// 将目标值反向转换回源值。
    /// </summary>
    /// <param name="value">待反向转换的值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">可选转换参数。</param>
    /// <param name="culture">当前区域信息。</param>
    /// <returns>反转后的布尔值；若输入无效则返回 <c>false</c>。</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : false;
    }
}

