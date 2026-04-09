using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

/// <summary>
/// 负责采集单帧点云、导出 CSV，并提取中位 Y 截面的服务。
/// </summary>
public interface IPointCloudMedianSectionCaptureService
{
    /// <summary>
    /// 从指定设备采集单帧点云，将 CSV 落盘到 Log 目录，并提取中位 Y 截面的二维 X/Z 点集。
    /// </summary>
    /// <param name="serialNumber">目标点云设备序列号。</param>
    /// <returns>包含导出 CSV 路径和中位截面提取结果的对象。</returns>
    PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(string serialNumber);
}
