using System;

namespace GrindCar.Models.PointCloud;

/// <summary>
/// 点云在线采集参数。
/// </summary>
public sealed class PointCloudCaptureSettings
{
    private const double FrameRateFactorForOneCentimeter = 100.0 / 60.0;

    public PointCloudCaptureSettings(double speedMetersPerMinute, int profileCount)
    {
        if (speedMetersPerMinute <= 0.0)
        {
            throw new InvalidOperationException("小车运行速度必须大于 0。");
        }

        if (profileCount <= 0)
        {
            throw new InvalidOperationException("单次测量总条数必须为正整数。");
        }

        SpeedMetersPerMinute = speedMetersPerMinute;
        ProfileCount = profileCount;
        FrameRateHz = CalculateFrameRateHz(speedMetersPerMinute);
    }

    public double SpeedMetersPerMinute { get; }

    public int ProfileCount { get; }

    public double FrameRateHz { get; }

    public static double CalculateFrameRateHz(double speedMetersPerMinute)
    {
        if (speedMetersPerMinute <= 0.0)
        {
            throw new InvalidOperationException("小车运行速度必须大于 0。");
        }

        return speedMetersPerMinute * FrameRateFactorForOneCentimeter;
    }
}
