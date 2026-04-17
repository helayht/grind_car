using System.Globalization;

namespace GrindCar.Services.Motor;

/// <summary>
/// 电机调试输入解析。
/// </summary>
public static class MotorInputParser
{
    public static bool TryParseNumber(string input, out double value)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseBool(string input, out bool value)
    {
        string normalized = input.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "1":
            case "true":
            case "on":
            case "yes":
            case "y":
            case "是":
                value = true;
                return true;
            case "0":
            case "false":
            case "off":
            case "no":
            case "n":
            case "否":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}