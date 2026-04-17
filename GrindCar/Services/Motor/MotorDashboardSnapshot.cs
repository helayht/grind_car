using System;

namespace GrindCar.Services.Motor;

/// <summary>
/// 首页仪表盘快照。
/// </summary>
public readonly struct MotorDashboardSnapshot
{
    public MotorDashboardSnapshot(double batteryLevel, double batteryVoltage, double currentSpeed, string speedStatus, string timestamp)
    {
        BatteryLevel = batteryLevel;
        BatteryVoltage = batteryVoltage;
        CurrentSpeed = currentSpeed;
        SpeedStatus = speedStatus;
        Timestamp = timestamp;
    }

    public double BatteryLevel { get; }

    public double BatteryVoltage { get; }

    public double CurrentSpeed { get; }

    public string SpeedStatus { get; }

    public string Timestamp { get; }
}