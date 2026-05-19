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

    public static double ParsePositiveDouble(string? rawValue, string parameterName)
    {
        double value = ParseDouble(rawValue, parameterName);
        if (value <= 0.0)
        {
            throw new InvalidOperationException($"{parameterName}必须大于 0。");
        }

        return value;
    }

    public static int ParsePositiveInt32(string? rawValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException($"{parameterName}不能为空。");
        }

        string text = rawValue.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) &&
            !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            throw new InvalidOperationException($"{parameterName}格式无效。");
        }

        if (value <= 0)
        {
            throw new InvalidOperationException($"{parameterName}必须为正整数。");
        }

        return value;
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

    private static double ParseDouble(string? rawValue, string parameterName)
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
}
