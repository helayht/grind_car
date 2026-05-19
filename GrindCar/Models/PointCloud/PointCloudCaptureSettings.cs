using System;

namespace GrindCar.Models.PointCloud;

/// <summary>
/// 点云在线采集参数。
/// </summary>
public sealed class PointCloudCaptureSettings
{
    private const double FrameRateFactorForOneCentimeter = 250.0 / 9.0;

    public PointCloudCaptureSettings(double speedKmPerHour, int profileCount)
    {
        if (speedKmPerHour <= 0.0)
        {
            throw new InvalidOperationException("小车运行速度必须大于 0。");
        }

        if (profileCount <= 0)
        {
            throw new InvalidOperationException("单次测量总条数必须为正整数。");
        }

        SpeedKmPerHour = speedKmPerHour;
        ProfileCount = profileCount;
        FrameRateHz = CalculateFrameRateHz(speedKmPerHour);
    }

    public double SpeedKmPerHour { get; }

    public int ProfileCount { get; }

    public double FrameRateHz { get; }

    public static double CalculateFrameRateHz(double speedKmPerHour)
    {
        if (speedKmPerHour <= 0.0)
        {
            throw new InvalidOperationException("小车运行速度必须大于 0。");
        }

        return speedKmPerHour * FrameRateFactorForOneCentimeter;
    }
}
