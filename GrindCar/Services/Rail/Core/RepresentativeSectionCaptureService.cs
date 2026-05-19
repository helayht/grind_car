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
        PointCloudCaptureSettings captureSettings = new PointCloudCaptureSettingsStore().LoadRequired();
        return CaptureRepresentativeSectionPoints(captureSettings);
    }

    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints(PointCloudCaptureSettings captureSettings)
    {
        IPointCloudMedianSectionCaptureService medianSectionCaptureService = new PointCloudMedianSectionCaptureService();
        PointCloudExportService pointCloudExportService = new PointCloudExportService();
        PointCloudDeviceConfigurationStore configurationStore = new();

        if (captureSettings == null)
        {
            throw new ArgumentNullException(nameof(captureSettings));
        }

        IReadOnlyList<PointCloudDeviceInfo> pointCloudDeviceInfos = pointCloudExportService.GetDevices();

        if (pointCloudDeviceInfos.Count == 0)
        {
            throw new PointCloudSdkException("未找到任何点云设备。");
        }

        IReadOnlyDictionary<string, PointCloudDeviceSide> deviceSideMap = configurationStore.LoadDeviceSideMap();
        var mergedPoints = new List<RailProfilePoint>();
        for (int index = 0; index < pointCloudDeviceInfos.Count; index++)
        {
            PointCloudDeviceInfo pointCloudDeviceInfo = pointCloudDeviceInfos[index];
            PointCloudDeviceSide side = PointCloudDeviceConfigurationStore.ResolveConfiguredSide(
                deviceSideMap,
                pointCloudDeviceInfo.SerialNumber);

            PointCloudMedianSectionCaptureResult result =
                medianSectionCaptureService.CaptureMedianSectionProfile(pointCloudDeviceInfo.SerialNumber, side, captureSettings);

            if (result.ExtractionResult.ProfilePoints.Count == 0)
            {
                continue;
            }

            mergedPoints.AddRange(result.ExtractionResult.ProfilePoints);
        }

        if (mergedPoints.Count == 0)
        {
            throw new InvalidOperationException("未能从任何点云设备提取到有效的代表截面点。");
        }

        return mergedPoints;
    }
}
