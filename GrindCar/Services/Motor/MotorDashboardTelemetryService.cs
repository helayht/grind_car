using System;
using System.Globalization;

namespace GrindCar.Services.Motor;

/// <summary>
/// 提供首页仪表盘演示数据。
/// </summary>
public class MotorDashboardTelemetryService
{
    private const double BatteryMinPercent = 18.0;
    private const double BatteryMaxPercent = 96.0;
    private const double BatteryVoltageMin = 44.8;
    private const double BatteryVoltageMax = 53.6;

    private int _tickIndex;

    public MotorDashboardSnapshot CreateInitialSnapshot()
    {
        return new MotorDashboardSnapshot(
            batteryLevel: 78.0,
            batteryVoltage: 50.4,
            currentSpeed: 12.6,
            speedStatus: "匀速巡航",
            timestamp: DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
    }

    public MotorDashboardSnapshot NextSnapshot()
    {
        _tickIndex++;
        double batteryPhase = Math.Sin(_tickIndex * 0.32);
        double speedPhase = Math.Sin(_tickIndex * 0.45);

        double batteryLevel = Math.Round(57.0 + batteryPhase * 39.0, 0);
        double batteryVoltage = Math.Round(
            BatteryVoltageMin +
            (batteryLevel - BatteryMinPercent) / (BatteryMaxPercent - BatteryMinPercent) * (BatteryVoltageMax - BatteryVoltageMin),
            1);
        double currentSpeed = Math.Round(10.7 + speedPhase * 5.9, 1);
        string speedStatus = currentSpeed switch
        {
            < 6.0 => "低速调整",
            < 13.0 => "匀速巡航",
            _ => "高速通过"
        };

        return new MotorDashboardSnapshot(
            batteryLevel,
            batteryVoltage,
            currentSpeed,
            speedStatus,
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
    }
}