using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using GrindCar.Models.PointCloud;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.PointCloud;

/// <summary>
/// 封装点云设备 SDK 的设备枚举、单帧采集与导出能力。
/// </summary>
public sealed class PointCloudExportService
{
    private const uint DefaultGetImageTimeoutMs = 3000;
    private const uint OriginImageModeValue = 1;
    private const uint PointCloudImageModeValue = 4;
    private const uint RangeImageModeValue = 7;
    private const uint IntensityImageModeValue = 10;
    private const int PointCoordinateCount = 3;
    private const int FloatPointSizeInBytes = sizeof(float) * PointCoordinateCount;
    private const int Int16PointSizeInBytes = sizeof(short) * PointCoordinateCount;

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
        ExportPointCloud(serialNumber, outputPath, exportFormat, null);
    }

    internal void ExportPointCloud(
        string serialNumber,
        string outputPath,
        PointCloudExportFormat exportFormat,
        PointCloudCaptureSettings? captureSettings)
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

        string normalizedOutputPath = Path.GetFullPath(outputPath);
        string sdkOutputPath = BuildSdkOutputPath(normalizedOutputPath, exportFormat);
        Directory.CreateDirectory(outputDirectory);

        _ = ExecuteWithSdkLifecycle(() =>
        {
            IntPtr deviceHandle = IntPtr.Zero;
            bool measurementStarted = false;

            using var imageModeParam = new MV3D_LP_PARAM();
            using var imageModeValue = new MV3D_LP_ENUMPARAM();
            using var pointCloudImage = new MV3D_LP_IMAGE_DATA();

            try
            {
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref deviceHandle, serialNumber), $"打开设备失败，SN: {serialNumber}");

                imageModeValue.nCurValue = PointCloudImageModeValue;
                imageModeParam.set_enumparam(imageModeValue);
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_SetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_ENUM_IMAGEMODE, imageModeParam), "设置 3D 点云模式失败。");

                if (captureSettings != null)
                {
                    ApplyCaptureSettings(deviceHandle, captureSettings);
                }

                EnsureSuccess(Mv3dLpSDK.MV3D_LP_StartMeasure(deviceHandle), "启动测量失败。");
                measurementStarted = true;

                EnsureSuccess(Mv3dLpSDK.MV3D_LP_GetImage(deviceHandle, pointCloudImage, DefaultGetImageTimeoutMs), "获取点云数据失败。");
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_SaveImage(pointCloudImage, ToSdkFileType(exportFormat), sdkOutputPath), "导出点云文件失败。");
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

        EnsureExportedFileAtExpectedPath(normalizedOutputPath, sdkOutputPath, exportFormat);
    }

    /// <summary>
    /// 从指定设备在线采集单帧点云并直接返回三维点集合（不落盘 CSV）。
    /// </summary>
    /// <param name="serialNumber">目标设备序列号。</param>
    /// <returns>采集到的三维点列表。</returns>
    internal IReadOnlyList<PointCloudPoint3D> CapturePointCloudPoints(string serialNumber)
    {
        PointCloudCaptureSettings settings = new PointCloudCaptureSettingsStore().LoadRequired();
        return CapturePointCloudPoints(serialNumber, settings);
    }

    /// <summary>
    /// 从指定设备在线采集单帧点云并直接返回三维点集合（不落盘 CSV）。
    /// </summary>
    /// <param name="serialNumber">目标设备序列号。</param>
    /// <param name="captureSettings">点云在线采集参数。</param>
    /// <returns>采集到的三维点列表。</returns>
    internal IReadOnlyList<PointCloudPoint3D> CapturePointCloudPoints(
        string serialNumber,
        PointCloudCaptureSettings captureSettings)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new PointCloudSdkException("未选择设备序列号。");
        }

        if (captureSettings == null)
        {
            throw new ArgumentNullException(nameof(captureSettings));
        }

        return ExecuteWithSdkLifecycle<IReadOnlyList<PointCloudPoint3D>>(() =>
        {
            IntPtr deviceHandle = IntPtr.Zero;
            bool measurementStarted = false;
            using var imageModeParam = new MV3D_LP_PARAM();
            using var imageModeValue = new MV3D_LP_ENUMPARAM();
            using var pointCloudImage = new MV3D_LP_IMAGE_DATA();

            try
            {
                EnsureSuccess(Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref deviceHandle, serialNumber), $"打开设备失败，SN: {serialNumber}");

                imageModeValue.nCurValue = PointCloudImageModeValue;
                imageModeParam.set_enumparam(imageModeValue);
                EnsureSuccess(
                    Mv3dLpSDK.MV3D_LP_SetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_ENUM_IMAGEMODE, imageModeParam),
                    "设置 3D 点云模式失败。");

                ApplyCaptureSettings(deviceHandle, captureSettings);

                EnsureSuccess(Mv3dLpSDK.MV3D_LP_StartMeasure(deviceHandle), "启动测量失败。");
                measurementStarted = true;

                EnsureSuccess(Mv3dLpSDK.MV3D_LP_GetImage(deviceHandle, pointCloudImage, DefaultGetImageTimeoutMs), "获取点云数据失败。");

                return DecodePointCloudImage(pointCloudImage);
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
        });
    }

    private static void ApplyCaptureSettings(IntPtr deviceHandle, PointCloudCaptureSettings captureSettings)
    {
        using var frameRateReadParam = new MV3D_LP_PARAM();
        EnsureSuccess(
            Mv3dLpSDK.MV3D_LP_GetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_FLOAT_FRAMERATE, frameRateReadParam),
            "读取采集帧率范围失败。");
        MV3D_LP_FLOATPARAM frameRateRange = frameRateReadParam.get_floatparam();
        EnsureFrameRateInRange(captureSettings.FrameRateHz, frameRateRange.fMin, frameRateRange.fMax);

        using var heightReadParam = new MV3D_LP_PARAM();
        EnsureSuccess(
            Mv3dLpSDK.MV3D_LP_GetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_INT_HEIGHT, heightReadParam),
            "读取单次测量总条数范围失败。");
        MV3D_LP_INTPARAM heightRange = heightReadParam.get_intparam();
        EnsureProfileCountInRange(captureSettings.ProfileCount, heightRange.nMin, heightRange.nMax, heightRange.nInc);

        using var frameRateWriteParam = new MV3D_LP_PARAM();
        using var frameRateValue = new MV3D_LP_FLOATPARAM();
        frameRateValue.fCurValue = (float)captureSettings.FrameRateHz;
        frameRateWriteParam.set_floatparam(frameRateValue);
        EnsureSuccess(
            Mv3dLpSDK.MV3D_LP_SetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_FLOAT_FRAMERATE, frameRateWriteParam),
            "设置采集帧率失败。");

        using var heightWriteParam = new MV3D_LP_PARAM();
        using var heightValue = new MV3D_LP_INTPARAM();
        heightValue.nCurValue = captureSettings.ProfileCount;
        heightWriteParam.set_intparam(heightValue);
        EnsureSuccess(
            Mv3dLpSDK.MV3D_LP_SetParam(deviceHandle, Mv3dLpSDK.MV3D_LP_INT_HEIGHT, heightWriteParam),
            "设置单次测量总条数失败。");
    }

    internal static void EnsureFrameRateInRange(double frameRateHz, double minFrameRateHz, double maxFrameRateHz)
    {
        if (frameRateHz < minFrameRateHz || frameRateHz > maxFrameRateHz)
        {
            throw new PointCloudSdkException(
                $"计算帧率 {FormatNumber(frameRateHz)}Hz 超出设备支持范围 {FormatNumber(minFrameRateHz)}~{FormatNumber(maxFrameRateHz)}Hz。");
        }
    }

    internal static void EnsureProfileCountInRange(int profileCount, long minProfileCount, long maxProfileCount, long increment)
    {
        if (profileCount < minProfileCount || profileCount > maxProfileCount)
        {
            throw new PointCloudSdkException(
                $"单次测量总条数 {profileCount.ToString(CultureInfo.InvariantCulture)} 超出设备 Height 支持范围 {minProfileCount.ToString(CultureInfo.InvariantCulture)}~{maxProfileCount.ToString(CultureInfo.InvariantCulture)}。");
        }

        if (increment > 1 && (profileCount - minProfileCount) % increment != 0)
        {
            throw new PointCloudSdkException(
                $"单次测量总条数 {profileCount.ToString(CultureInfo.InvariantCulture)} 不满足设备 Height 步进 {increment.ToString(CultureInfo.InvariantCulture)}。");
        }
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
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
    /// 生成传给 SDK 的导出路径。
    /// 某些 SDK 文件类型会自动补扩展名，因此这里传入不带目标扩展名的基路径，避免出现 .csv.csv。
    /// </summary>
    /// <param name="outputPath">调用方期望的最终导出路径。</param>
    /// <param name="exportFormat">导出格式。</param>                            
    /// <returns>适合传给 SDK 的输出路径。</returns>
    private static string BuildSdkOutputPath(string outputPath, PointCloudExportFormat exportFormat)
    {
        string expectedExtension = GetExpectedExtension(exportFormat);
        return string.Equals(Path.GetExtension(outputPath), expectedExtension, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath))
            : outputPath;
    }

    /// <summary>
    /// 确保导出完成后，调用方期望的路径上存在最终文件。
    /// </summary>
    /// <param name="expectedOutputPath">调用方期望的最终路径。</param>
    /// <param name="sdkOutputPath">传给 SDK 的输出路径。</param>
    /// <param name="exportFormat">导出格式。</param>
    private static void EnsureExportedFileAtExpectedPath(
        string expectedOutputPath,
        string sdkOutputPath,
        PointCloudExportFormat exportFormat)
    {
        if (File.Exists(expectedOutputPath))
        {
            return;
        }

        string expectedExtension = GetExpectedExtension(exportFormat);
        string sdkGeneratedPath = sdkOutputPath + expectedExtension;

        if (File.Exists(sdkGeneratedPath))
        {
            File.Move(sdkGeneratedPath, expectedOutputPath, true);
            return;
        }

        if (File.Exists(sdkOutputPath))
        {
            File.Move(sdkOutputPath, expectedOutputPath, true);
            return;
        }

        throw new PointCloudSdkException($"点云导出完成后未找到文件: {expectedOutputPath}");
    }

    /// <summary>
    /// 获取指定导出格式的标准文件扩展名。
    /// </summary>
    /// <param name="exportFormat">导出格式。</param>
    /// <returns>对应的小写扩展名。</returns>
    private static string GetExpectedExtension(PointCloudExportFormat exportFormat)
    {
        return exportFormat switch
        {
            PointCloudExportFormat.Ply => ".ply",
            PointCloudExportFormat.Csv => ".csv",
            PointCloudExportFormat.Obj => ".obj",
            _ => throw new PointCloudSdkException($"不支持的导出格式: {exportFormat}")
        };
    }

    internal static IReadOnlyList<PointCloudPoint3D> DecodePointCloudImage(MV3D_LP_IMAGE_DATA pointCloudImage)
    {
        if (pointCloudImage == null)
        {
            throw new PointCloudSdkException("点云图像数据为空。");
        }

        if (pointCloudImage.pData == IntPtr.Zero || pointCloudImage.nDataLen == 0)
        {
            throw new PointCloudSdkException("点云图像数据缓冲区为空。");
        }

        ulong pointCountByShape = (ulong)pointCloudImage.nWidth * pointCloudImage.nHeight;
        int dataLength = checked((int)pointCloudImage.nDataLen);
        DecodeFormat decodeFormat = ResolveDecodeFormat(dataLength, pointCountByShape);

        if (decodeFormat.PointCount <= 0)
        {
            return Array.Empty<PointCloudPoint3D>();
        }

        return decodeFormat.Kind == PointCloudCoordinateFormat.Float
            ? DecodeFloatPointCloud(pointCloudImage, decodeFormat.PointCount)
            : DecodeInt16PointCloud(pointCloudImage, decodeFormat.PointCount);
    }

    private static DecodeFormat ResolveDecodeFormat(int dataLength, ulong pointCountByShape)
    {
        if (pointCountByShape > 0)
        {
            ulong expectedFloatLength = pointCountByShape * FloatPointSizeInBytes;
            ulong expectedInt16Length = pointCountByShape * Int16PointSizeInBytes;

            if ((ulong)dataLength == expectedFloatLength)
            {
                return new DecodeFormat(PointCloudCoordinateFormat.Float, checked((int)pointCountByShape));
            }

            if ((ulong)dataLength == expectedInt16Length)
            {
                return new DecodeFormat(PointCloudCoordinateFormat.Int16, checked((int)pointCountByShape));
            }

            throw new PointCloudSdkException(
                $"点云数据长度与图像尺寸不匹配，无法识别坐标格式。WidthHeight={pointCountByShape}, DataLen={dataLength}");
        }

        bool canDecodeAsFloat = dataLength % FloatPointSizeInBytes == 0;
        bool canDecodeAsInt16 = dataLength % Int16PointSizeInBytes == 0;

        if (canDecodeAsFloat && canDecodeAsInt16)
        {
            throw new PointCloudSdkException(
                $"点云数据缺少有效宽高且长度存在格式歧义，无法识别坐标格式。DataLen={dataLength}");
        }

        if (canDecodeAsFloat)
        {
            return new DecodeFormat(PointCloudCoordinateFormat.Float, dataLength / FloatPointSizeInBytes);
        }

        if (canDecodeAsInt16)
        {
            return new DecodeFormat(PointCloudCoordinateFormat.Int16, dataLength / Int16PointSizeInBytes);
        }

        throw new PointCloudSdkException($"点云数据长度异常，无法识别坐标格式。DataLen={dataLength}");
    }

    private static IReadOnlyList<PointCloudPoint3D> DecodeFloatPointCloud(MV3D_LP_IMAGE_DATA pointCloudImage, int pointCount)
    {
        var rawValues = new float[pointCount * PointCoordinateCount];
        Marshal.Copy(pointCloudImage.pData, rawValues, 0, rawValues.Length);

        var points = new List<PointCloudPoint3D>(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            int baseIndex = index * PointCoordinateCount;
            double x = rawValues[baseIndex];
            double y = rawValues[baseIndex + 1];
            double z = rawValues[baseIndex + 2];

            if (!IsFinitePoint(x, y, z))
            {
                continue;
            }

            points.Add(new PointCloudPoint3D(x, y, z));
        }

        return points;
    }

    private static IReadOnlyList<PointCloudPoint3D> DecodeInt16PointCloud(MV3D_LP_IMAGE_DATA pointCloudImage, int pointCount)
    {
        var rawValues = new short[pointCount * PointCoordinateCount];
        Marshal.Copy(pointCloudImage.pData, rawValues, 0, rawValues.Length);

        float xScale = pointCloudImage.fXScale;
        float yScale = pointCloudImage.fYScale;
        float zScale = pointCloudImage.fZScale;
        int xOffset = pointCloudImage.nXOffset;
        int yOffset = pointCloudImage.nYOffset;
        int zOffset = pointCloudImage.nZOffset;

        var points = new List<PointCloudPoint3D>(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            int baseIndex = index * PointCoordinateCount;
            double x = rawValues[baseIndex] * xScale + xOffset;
            double y = rawValues[baseIndex + 1] * yScale + yOffset;
            double z = rawValues[baseIndex + 2] * zScale + zOffset;

            if (!IsFinitePoint(x, y, z))
            {
                continue;
            }

            points.Add(new PointCloudPoint3D(x, y, z));
        }

        return points;
    }

    private static bool IsFinitePoint(double x, double y, double z)
    {
        return !(double.IsNaN(x) || double.IsInfinity(x) ||
                 double.IsNaN(y) || double.IsInfinity(y) ||
                 double.IsNaN(z) || double.IsInfinity(z));
    }

    private enum PointCloudCoordinateFormat
    {
        Float,
        Int16
    }

    private readonly record struct DecodeFormat(PointCloudCoordinateFormat Kind, int PointCount);

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
