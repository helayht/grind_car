using System;
using System.Globalization;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 测量参数输入解析器。
/// </summary>
public static class MeasurementInputParser
{
    public static double ParsePosition(string? rawValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException($"{parameterName}不能为空。");
        }

        string text = rawValue.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }

        throw new InvalidOperationException($"{parameterName}格式无效。");
    }

    public static int ToScaledInt32(double value, double scale, string parameterName)
    {
        double scaled = value * scale;
        if (scaled > int.MaxValue || scaled < int.MinValue)
        {
            throw new InvalidOperationException($"{parameterName}超出 Int32 范围。");
        }

        return (int)Math.Round(scaled);
    }
}