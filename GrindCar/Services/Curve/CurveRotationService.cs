using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace GrindCar.Services.Curve;

/// <summary>
/// 曲线旋转服务。
/// </summary>
public static class CurveRotationService
{
    public static bool TryParseAngle(string input, out double angleDegrees)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out angleDegrees))
        {
            return true;
        }

        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out angleDegrees);
    }

    public static List<Point> RotateCounterclockwise(IReadOnlyList<Point> points, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var rotated = new List<Point>(points.Count);

        for (int index = 0; index < points.Count; index++)
        {
            Point point = points[index];
            double rotatedX = point.X * cosValue - point.Y * sinValue;
            double rotatedY = point.X * sinValue + point.Y * cosValue;
            rotated.Add(new Point(rotatedX, rotatedY));
        }

        return rotated;
    }
}