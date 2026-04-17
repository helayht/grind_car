using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 中位截面调试业务编排服务。
/// </summary>
public class MedianSectionDebugWorkflowService
{
    private readonly IPointCloudRepresentativeProfileService _profileService;

    public MedianSectionDebugWorkflowService(IPointCloudRepresentativeProfileService? profileService = null)
    {
        _profileService = profileService ?? new PointCloudRepresentativeProfileService();
    }

    public MedianSectionExtractionResult Extract(string filePath)
    {
        return _profileService.ExtractMedianSectionProfileFromCsv(filePath);
    }

    public void Export(string outputPath, double medianY, IReadOnlyList<RailProfilePoint> points)
    {
        MedianSectionCsvExporter.Export(outputPath, medianY, points);
    }
}
