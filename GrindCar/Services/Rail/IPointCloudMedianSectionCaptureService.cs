using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services.Rail;

/// <summary>
/// 负责采集单帧点云、导出 CSV，并提取代表截面的服务。
/// </summary>
public interface IPointCloudMedianSectionCaptureService
{
    /// <summary>
    /// 从指定设备采集单帧点云，将 CSV 落盘到 Log 目录，并提取平均代表截面的二维 X/Z 点集。
    /// </summary>
    /// <param name="serialNumber">目标点云设备序列号。</param>
    /// <param name="side">目标点云设备对应的轨面半边。</param>
    /// <returns>包含导出 CSV 路径和代表截面提取结果的对象。</returns>
    PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(string serialNumber, PointCloudDeviceSide side);

    /// <summary>
    /// 从指定设备按给定采集参数采集单帧点云，并提取平均代表截面的二维 X/Z 点集。
    /// </summary>
    /// <param name="serialNumber">目标点云设备序列号。</param>
    /// <param name="side">目标点云设备对应的轨面半边。</param>
    /// <param name="captureSettings">点云在线采集参数。</param>
    /// <param name="archiveContext">测量流程原始点云留档上下文；为空时不留档。</param>
    /// <returns>包含导出 CSV 路径和代表截面提取结果的对象。</returns>
    PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(
        string serialNumber,
        PointCloudDeviceSide side,
        PointCloudCaptureSettings captureSettings,
        MeasurementPointCloudArchiveContext? archiveContext = null);
}
