namespace GrindCar.Models.PointCloud;

/// <summary>
/// 表示一个可连接的点云设备信息。
/// </summary>
public sealed class PointCloudDeviceInfo
{
    /// <summary>
    /// 初始化点云设备信息。
    /// </summary>
    /// <param name="serialNumber">设备序列号。</param>
    /// <param name="modelName">设备型号。</param>
    /// <param name="currentIp">设备当前 IP。</param>
    public PointCloudDeviceInfo(string serialNumber, string modelName, string currentIp)
    {
        SerialNumber = serialNumber;
        ModelName = modelName;
        CurrentIp = currentIp;
    }

    public string SerialNumber { get; }

    public string ModelName { get; }

    public string CurrentIp { get; }

    public string DisplayName => $"{ModelName} | SN: {SerialNumber} | IP: {CurrentIp}";
}
