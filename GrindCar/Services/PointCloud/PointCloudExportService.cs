using System;
using System.Collections.Generic;
using System.IO;
using GrindCar.Models.PointCloud;

namespace GrindCar.Services.PointCloud;

public sealed class PointCloudExportService
{
    private const uint DefaultGetImageTimeoutMs = 3000;
    private const uint RangeImageModeValue = 4;

    public IReadOnlyList<PointCloudDeviceInfo> GetDevices()
    {
        return ExecuteWithSdkLifecycle<IReadOnlyList<PointCloudDeviceInfo>>(() =>
        {
            uint deviceCount = 0;
            EnsureSuccess(Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref deviceCount), "获取设备数量失败。");
            if (deviceCount == 0)
            {
                return Array.Empty<PointCloudDeviceInfo>();
            }

            using var deviceVector = CreateDeviceVector(deviceCount);
            uint queriedCount = deviceCount;
            EnsureSuccess(Mv3dLpSDK.MV3D_LP_GetDeviceList(deviceVector[0], queriedCount, ref queriedCount), "获取设备列表失败。");

            var devices = new List<PointCloudDeviceInfo>((int)queriedCount);
            for (int index = 0; index < queriedCount; index++)
            {
                MV3D_LP_DEVICE_INFO device = deviceVector[index];
                devices.Add(new PointCloudDeviceInfo(
                    device.chSerialNumber ?? string.Empty,
                    device.chModelName ?? "Unknown",
                    device.chCurrentIp ?? "Unknown"));
            }

            return devices;
        });
    }

    public void ExportPointCloud(string serialNumber, string outputPath, PointCloudExportFormat exportFormat)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new PointCloudSdkException("未选择设备序列号。");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new PointCloudSdkException("导出路径不能为空。");
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new PointCloudSdkException("导出路径无效。");
        }

        Directory.CreateDirectory(outputDirectory);

        _ = ExecuteWithSdkLifecycle(() =>
        {
            IntPtr deviceHandle = IntPtr.Zero;
            bool measurementStarted = false;

            using var imageModeParam = new MV3D_LP_PARAM();
            using var imageModeValue = new MV3D_LP_ENUMPARAM();
            using var depthImage = new MV3D_LP_IMAGE_DATA();
            using var pointCloudImage = new MV3D_LP_IMAGE_DATA();

            try
            {
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref deviceHandle, serialNumber), $"打开设备失败，SN: {serialNumber}");

                imageModeValue.nCurValue = RangeImageModeValue;
                imageModeParam.set_enumparam(imageModeValue);
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_SetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_ENUM_IMAGEMODE, imageModeParam), "设置图像模式失败。");

                EnsureSuccess(Mv3dLpSDK.MV3D_LP_StartMeasure(deviceHandle), "启动测量失败。");
                measurementStarted = true;

                EnsureSuccess(Mv3dLpSDK.MV3D_LP_GetImage(deviceHandle, depthImage, DefaultGetImageTimeoutMs), "获取深度图失败。");
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_MapDepthToPointCloud(depthImage, pointCloudImage), "深度图转换点云失败。");
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_SaveImage(pointCloudImage, ToSdkFileType(exportFormat), outputPath), "导出点云文件失败。");
            }
            finally
            {
                if (measurementStarted)
                {
                    TryExecute(() => Mv3dLpSDK.MV3D_LP_StopMeasure(deviceHandle));
                }

                if (deviceHandle != IntPtr.Zero)
                {
                    TryExecute(() => Mv3dLpSDK.MV3D_LP_CloseDevice(ref deviceHandle));
                }
            }

            return true;
        });
    }

    private static MV3D_LP_DEVICE_INFO_VECTOR CreateDeviceVector(uint deviceCount)
    {
        var deviceVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)deviceCount);
        for (int index = 0; index < deviceCount; index++)
        {
            deviceVector.Add(new MV3D_LP_DEVICE_INFO());
        }

        return deviceVector;
    }

    private static uint ToSdkFileType(PointCloudExportFormat exportFormat)
    {
        return exportFormat switch
        {
            PointCloudExportFormat.Ply => Mv3dLpSDK.FileType_PLY,
            PointCloudExportFormat.Csv => Mv3dLpSDK.FileType_CSV,
            PointCloudExportFormat.Obj => Mv3dLpSDK.FileType_OBJ,
            _ => throw new PointCloudSdkException($"不支持的导出格式: {exportFormat}")
        };
    }

    private static T ExecuteWithSdkLifecycle<T>(Func<T> action)
    {
        try
        {
            EnsureSuccess(Mv3dLpSDK.MV3D_LP_Initialize(), "初始化 SDK 失败。");
            return action();
        }
        catch (PointCloudSdkException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PointCloudSdkException("调用点云 SDK 时发生异常。", ex);
        }
        finally
        {
            TryExecute(Mv3dLpSDK.MV3D_LP_Finalize);
        }
    }

    private static void EnsureSuccess(int sdkResult, string message)
    {
        if (sdkResult == Mv3dLpSDK.MV3D_LP_OK)
        {
            return;
        }

        throw new PointCloudSdkException($"{message} SDK 返回码: {sdkResult}");
    }

    private static void TryExecute(Func<int> action)
    {
        try
        {
            _ = action();
        }
        catch
        {
        }
    }
}
