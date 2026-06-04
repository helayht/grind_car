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
        if (captureSettings == null)
        {
            throw new ArgumentNullException(nameof(captureSettings));
        }

        IReadOnlyList<ConfiguredPointCloudDevice> configuredDevices = GetAvailableConfiguredDevicesInOrder();
        var mergedPoints = new List<RailProfilePoint>();
        for (int index = 0; index < configuredDevices.Count; index++)
        {
            mergedPoints.AddRange(CaptureRepresentativeSectionPoints(configuredDevices[index], captureSettings));
        }

        if (mergedPoints.Count == 0)
        {
            throw new InvalidOperationException("未能从任何点云设备提取到有效的代表截面点。");
        }

        return mergedPoints;
    }

    public static IReadOnlyList<ConfiguredPointCloudDevice> GetAvailableConfiguredDevicesInOrder()
    {
        PointCloudExportService pointCloudExportService = new();
        PointCloudDeviceConfigurationStore configurationStore = new();

        IReadOnlyList<PointCloudDeviceInfo> pointCloudDeviceInfos = pointCloudExportService.GetDevices();
        if (pointCloudDeviceInfos.Count == 0)
        {
            throw new PointCloudSdkException("未找到任何点云设备。");
        }

        var availableSerialNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < pointCloudDeviceInfos.Count; index++)
        {
            availableSerialNumbers.Add(pointCloudDeviceInfos[index].SerialNumber);
        }

        IReadOnlyList<ConfiguredPointCloudDevice> configuredDevices = configurationStore.LoadDeviceConfigurations();
        var availableConfiguredDevices = new List<ConfiguredPointCloudDevice>(configuredDevices.Count);
        for (int index = 0; index < configuredDevices.Count; index++)
        {
            ConfiguredPointCloudDevice device = configuredDevices[index];
            if (!availableSerialNumbers.Contains(device.SerialNumber))
            {
                throw new InvalidOperationException($"点云设备 {device.SerialNumber} 已配置但当前未找到。");
            }

            availableConfiguredDevices.Add(device);
        }

        return availableConfiguredDevices;
    }

    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints(
        ConfiguredPointCloudDevice device,
        PointCloudCaptureSettings captureSettings)
    {
        if (string.IsNullOrWhiteSpace(device.SerialNumber))
        {
            throw new InvalidOperationException("点云设备序列号为空，无法采集代表截面。");
        }

        if (captureSettings == null)
        {
            throw new ArgumentNullException(nameof(captureSettings));
        }

        IPointCloudMedianSectionCaptureService medianSectionCaptureService = new PointCloudMedianSectionCaptureService();
        PointCloudMedianSectionCaptureResult result =
            medianSectionCaptureService.CaptureMedianSectionProfile(device.SerialNumber, device.Side, captureSettings);

        return result.ExtractionResult.ProfilePoints;
    }
}
