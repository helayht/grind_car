namespace GrindCar.Models.PointCloud;

public sealed class PointCloudDeviceInfo
{
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
