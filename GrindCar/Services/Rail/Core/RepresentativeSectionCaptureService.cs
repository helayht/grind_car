using System;
using System.Collections.Generic;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 从点云设备采集代表截面点。
/// </summary>
public static class RepresentativeSectionCaptureService
{
    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints()
    {
        IPointCloudMedianSectionCaptureService medianSectionCaptureService = new PointCloudMedianSectionCaptureService();
        PointCloudExportService pointCloudExportService = new PointCloudExportService();
        IReadOnlyList<PointCloudDeviceInfo> pointCloudDeviceInfos = pointCloudExportService.GetDevices();

        if (pointCloudDeviceInfos.Count == 0)
        {
            throw new PointCloudSdkException("未找到任何点云设备。");
        }

        var mergedPoints = new List<RailProfilePoint>();
        for (int index = 0; index < pointCloudDeviceInfos.Count; index++)
        {
            PointCloudDeviceInfo pointCloudDeviceInfo = pointCloudDeviceInfos[index];
            PointCloudMedianSectionCaptureResult result =
                medianSectionCaptureService.CaptureMedianSectionProfile(pointCloudDeviceInfo.SerialNumber);

            if (result.ExtractionResult.ProfilePoints.Count == 0)
            {
                continue;
            }

            mergedPoints.AddRange(result.ExtractionResult.ProfilePoints);
        }

        if (mergedPoints.Count == 0)
        {
            throw new InvalidOperationException("未能从任何点云设备提取到有效的中位截面点。");
        }

        return mergedPoints;
    }
}