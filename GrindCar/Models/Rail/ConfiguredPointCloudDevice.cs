namespace GrindCar.Models.Rail;

/// <summary>
/// 表示已配置侧别的点云设备。
/// </summary>
/// <param name="SerialNumber">点云设备序列号。</param>
/// <param name="Side">点云设备对应的轨面半边。</param>
public readonly record struct ConfiguredPointCloudDevice(string SerialNumber, PointCloudDeviceSide Side);
