using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

/// <summary>
/// 负责采集单帧点云、导出 CSV，并提取中位 X 截面的服务。
/// </summary>
public interface IPointCloudMedianSectionCaptureService
{
    /// <summary>
    /// 从指定设备采集单帧点云，将 CSV 落盘到 Log 目录，并提取中位 X 截面的二维 Y/Z 点集。
    /// </summary>
    PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(string serialNumber);
}
