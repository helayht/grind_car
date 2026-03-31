using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

public interface IPointCloudRepresentativeProfileService
{
    /// <summary>
    /// 从点云 CSV 文件中提取中位 X 截面的二维 Y/Z 点集。
    /// 处理规则：
    /// 1. 先对全部 X 去重；
    /// 2. 再按偏左中位定义找到唯一 X 集合中的中位值；
    /// 3. 最后从原始点集中严格筛选 X 等于该中位值的点，并输出 (Y, Z)。
    /// </summary>
    MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath);

    /// <summary>
    /// 从点云 CSV 中提取能够代表该区段轨面廓形的二维点集。
    /// </summary>
    IReadOnlyList<RailProfilePoint> ExtractRepresentativeProfile(
        string csvPath,
        RepresentativeProfileExtractionOptions? options = null);

    /// <summary>
    /// 从点云 CSV 中提取代表廓形，并直接执行二维拟合。
    /// </summary>
    RailProfileFitResult FitRepresentativeProfile(
        string csvPath,
        RepresentativeProfileExtractionOptions? options = null);
}
