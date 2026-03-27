using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

public interface IPointCloudRepresentativeProfileService
{
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
