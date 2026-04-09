using System;
using System.Collections.Generic;
using System.IO;
using GrindCar.Models.PointCloud;

namespace GrindCar.Services.PointCloud;

/// <summary>
/// 封装点云设备 SDK 的设备枚举、单帧采集与导出能力。
/// </summary>
public sealed class PointCloudExportService
{
    private const uint DefaultGetImageTimeoutMs = 3000;
    private const uint RangeImageModeValue = 7;

    /// <summary>
    /// 获取当前可用的点云设备列表。
    /// </summary>
    /// <returns>扫描到的设备信息集合；若未发现设备则返回空集合。</returns>
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

    /// <summary>
    /// 从指定设备采集单帧点云并导出为目标文件。
    /// </summary>
    /// <param name="serialNumber">目标设备序列号。</param>
    /// <param name="outputPath">导出文件路径。</param>
    /// <param name="exportFormat">导出文件格式。</param>
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

    /// <summary>
    /// 创建指定容量的设备信息向量，供 SDK 写入设备列表。
    /// </summary>
    /// <param name="deviceCount">设备数量。</param>
    /// <returns>初始化后的设备向量对象。</returns>
    private static MV3D_LP_DEVICE_INFO_VECTOR CreateDeviceVector(uint deviceCount)
    {
        var deviceVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)deviceCount);
        for (int index = 0; index < deviceCount; index++)
        {
            deviceVector.Add(new MV3D_LP_DEVICE_INFO());
        }

        return deviceVector;
    }

    /// <summary>
    /// 将内部导出格式转换为 SDK 对应的文件类型编码。
    /// </summary>
    /// <param name="exportFormat">内部点云导出格式。</param>
    /// <returns>SDK 所需的文件类型编码。</returns>
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

    /// <summary>
    /// 在统一的 SDK 初始化和释放流程中执行指定操作。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="action">待执行的操作。</param>
    /// <returns>操作执行结果。</returns>
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

    /// <summary>
    /// 校验 SDK 返回码是否表示成功。
    /// </summary>
    /// <param name="sdkResult">SDK 返回码。</param>
    /// <param name="message">失败时使用的错误消息。</param>
    private static void EnsureSuccess(int sdkResult, string message)
    {
        if (sdkResult == Mv3dLpSDK.MV3D_LP_OK)
        {
            return;
        }

        throw new PointCloudSdkException($"{message} SDK 返回码: {sdkResult}");
    }

    /// <summary>
    /// 尝试执行清理动作，并忽略清理异常。
    /// </summary>
    /// <param name="action">待执行的清理动作。</param>
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
