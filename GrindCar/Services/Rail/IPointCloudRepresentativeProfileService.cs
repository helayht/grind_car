using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

public interface IPointCloudRepresentativeProfileService
{
    /// <summary>
    /// 从点云 CSV 文件中提取中位 Y 截面的二维 X/Z 点集。
    /// 处理规则：
    /// 1. 先对全部 Y 去重；
    /// 2. 再按偏左中位定义找到唯一 Y 集合中的中位值；
    /// 3. 最后从原始点集中严格筛选 Y 等于该中位值的点，并输出 (X, Z)。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <returns>包含中位 Y 值和截面点集的提取结果。</returns>
    MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath);

    /// <summary>
    /// 从点云 CSV 文件中提取中位 Y 截面的二维 X/Z 点集，并按设备侧别对齐到标准轨面边界。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <param name="side">点云设备对应的轨面半边。</param>
    /// <returns>包含中位 Y 值和对齐后截面点集的提取结果。</returns>
    MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath, PointCloudDeviceSide side);
}
