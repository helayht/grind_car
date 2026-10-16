using System.Globalization;

namespace GrindCar.Services.Motor;

/// <summary>
/// 电机调试输入解析。
/// </summary>
public static class MotorInputParser
{
    public static bool TryParseNumber(string input, out double value)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return double.IsFinite(value);
        }

        return false;
    }
}
