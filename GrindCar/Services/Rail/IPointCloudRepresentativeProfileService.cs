using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

public interface IPointCloudRepresentativeProfileService
{
    /// <summary>
    /// 从点云 CSV 文件中提取所有有效截面的算术平均二维 X/Z 点集。
    /// 处理规则：
    /// 1. 先过滤 X/Y/Z 全为 0 的异常点；
    /// 2. 再按 X 坐标聚合全部有效截面点；
    /// 3. 最后对每个 X 的 Z 坐标取算术平均，并输出 (X, AverageZ)。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <returns>包含代表 Y 值和平均截面点集的提取结果。</returns>
    MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath);

    /// <summary>
    /// 从点云 CSV 文件中提取所有有效截面的算术平均二维 X/Z 点集，并按设备侧别应用已保存的手动配准参数。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <param name="side">点云设备对应的轨面半边。</param>
    /// <returns>包含代表 Y 值和对齐后平均截面点集的提取结果。</returns>
    MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath, PointCloudDeviceSide side);
}
