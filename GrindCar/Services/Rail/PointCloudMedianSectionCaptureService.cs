using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

/// <summary>
/// 将 SDK 点云采集与代表截面提取串联起来。
/// 默认采用“在线采集点云 -> 直接提取截面”方式，必要时回退到 CSV。
/// </summary>
public sealed class PointCloudMedianSectionCaptureService : IPointCloudMedianSectionCaptureService
{
    private const string DefaultLogDirectoryName = "Log";
    private const string CsvFilePrefix = "point-cloud";
    private readonly IPointCloudExportService _pointCloudExportService;
    private readonly IPointCloudRepresentativeProfileService _representativeProfileService;
    private readonly string _logDirectoryPath;

    /// <summary>
    /// 使用默认日志目录和默认服务依赖初始化实例。
    /// </summary>
    public PointCloudMedianSectionCaptureService()
        : this(
            new PointCloudExportServiceAdapter(new PointCloudExportService()),
            new PointCloudRepresentativeProfileService(),
            Path.Combine(Environment.CurrentDirectory, DefaultLogDirectoryName))
    {
    }

    /// <summary>
    /// 使用指定依赖和日志目录初始化实例。
    /// </summary>
    /// <param name="pointCloudExportService">点云导出服务。</param>
    /// <param name="representativeProfileService">代表截面提取服务。</param>
    /// <param name="logDirectoryPath">CSV 回退模式的落盘目录。</param>
    public PointCloudMedianSectionCaptureService(
        PointCloudExportService pointCloudExportService,
        IPointCloudRepresentativeProfileService representativeProfileService,
        string logDirectoryPath)
        : this(
            new PointCloudExportServiceAdapter(pointCloudExportService),
            representativeProfileService,
            logDirectoryPath)
    {
    }

    internal PointCloudMedianSectionCaptureService(
        IPointCloudExportService pointCloudExportService,
        IPointCloudRepresentativeProfileService representativeProfileService,
        string logDirectoryPath)
    {
        _pointCloudExportService = pointCloudExportService ?? throw new ArgumentNullException(nameof(pointCloudExportService));
        _representativeProfileService = representativeProfileService ?? throw new ArgumentNullException(nameof(representativeProfileService));
        _logDirectoryPath = string.IsNullOrWhiteSpace(logDirectoryPath)
            ? throw new ArgumentException("Log 目录不能为空。", nameof(logDirectoryPath))
            : logDirectoryPath;
    }

    /// <summary>
    /// 采集指定设备的单帧点云并提取代表截面点集。
    /// </summary>
    /// <param name="serialNumber">目标设备序列号。</param>
    /// <returns>包含落盘 CSV 路径和提取结果的对象。</returns>
    public PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(string serialNumber, PointCloudDeviceSide side)
    {
        PointCloudCaptureSettings captureSettings = new PointCloudCaptureSettingsStore().LoadRequired();
        return CaptureMedianSectionProfile(serialNumber, side, captureSettings);
    }

    /// <summary>
    /// 采集指定设备的单帧点云并提取代表截面点集。
    /// </summary>
    /// <param name="serialNumber">目标设备序列号。</param>
    /// <param name="side">目标点云设备对应的轨面半边。</param>
    /// <param name="captureSettings">点云在线采集参数。</param>
    /// <returns>包含落盘 CSV 路径和提取结果的对象。</returns>
    public PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(
        string serialNumber,
        PointCloudDeviceSide side,
        PointCloudCaptureSettings captureSettings,
        MeasurementPointCloudArchiveContext? archiveContext = null)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new PointCloudSdkException("未选择设备序列号。");
        }

        if (captureSettings == null)
        {
            throw new ArgumentNullException(nameof(captureSettings));
        }

        if (_representativeProfileService is IPointCloudRepresentativeProfilePointExtractor representativeProfileService)
        {
            IReadOnlyList<PointCloudPoint3D>? points = null;
            try
            {
                points = _pointCloudExportService.CapturePointCloudPoints(serialNumber, captureSettings);
            }
            catch
            {
                // 在线读取失败时自动回退到 CSV 方案，避免阻断业务流程。
            }

            if (points != null && points.Count > 0)
            {
                MeasurementPointCloudArchiveService.QueueArchive(archiveContext, points);
                try
                {
                    MedianSectionExtractionResult onlineExtractionResult =
                        representativeProfileService.ExtractMedianSectionProfileFromPoints(points, side);
                    return new PointCloudMedianSectionCaptureResult(string.Empty, onlineExtractionResult);
                }
                catch (RepresentativeProfileExtractionException)
                {
                    // 在线提取失败时回退到 CSV，保留原有业务链路的可用性。
                }
            }
        }

        Directory.CreateDirectory(_logDirectoryPath);
        string csvPath = BuildCsvPath();
        _pointCloudExportService.ExportPointCloud(serialNumber, csvPath, PointCloudExportFormat.Csv, captureSettings);

        MedianSectionExtractionResult extractionResult =
            _representativeProfileService.ExtractMedianSectionProfileFromCsv(csvPath, side);

        return new PointCloudMedianSectionCaptureResult(csvPath, extractionResult);
    }

    /// <summary>
    /// 根据当前时间生成唯一的 CSV 输出路径。
    /// </summary>
    /// <returns>生成后的 CSV 文件完整路径。</returns>
    private string BuildCsvPath()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string fileName = $"{CsvFilePrefix}-{timestamp}.csv";
        return Path.Combine(_logDirectoryPath, fileName);
    }
}
