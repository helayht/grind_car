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
    private readonly MaximumDropProfileService? _maximumDropProfileService;
    private readonly IPointCloudProfileAnalysisService? _profileAnalysisService;
    private readonly string _logDirectoryPath;

    /// <summary>
    /// 使用默认日志目录和默认服务依赖初始化实例。
    /// </summary>
    public PointCloudMedianSectionCaptureService()
        : this(
            new PointCloudExportServiceAdapter(new PointCloudExportService()),
            new PointCloudRepresentativeProfileService(),
            new MaximumDropProfileService(),
            Path.Combine(Environment.CurrentDirectory, DefaultLogDirectoryName),
            new PointCloudProfileAnalysisService())
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
            new MaximumDropProfileService(),
            logDirectoryPath)
    {
    }

    internal PointCloudMedianSectionCaptureService(
        IPointCloudExportService pointCloudExportService,
        IPointCloudRepresentativeProfileService representativeProfileService,
        string logDirectoryPath)
        : this(pointCloudExportService, representativeProfileService, null, logDirectoryPath, null)
    {
    }

    internal PointCloudMedianSectionCaptureService(
        IPointCloudExportService pointCloudExportService,
        IPointCloudRepresentativeProfileService representativeProfileService,
        MaximumDropProfileService? maximumDropProfileService,
        string logDirectoryPath)
        : this(
            pointCloudExportService,
            representativeProfileService,
            maximumDropProfileService,
            logDirectoryPath,
            null)
    {
    }

    internal PointCloudMedianSectionCaptureService(
        IPointCloudExportService pointCloudExportService,
        IPointCloudRepresentativeProfileService representativeProfileService,
        MaximumDropProfileService? maximumDropProfileService,
        string logDirectoryPath,
        IPointCloudProfileAnalysisService? profileAnalysisService)
    {
        _pointCloudExportService = pointCloudExportService ?? throw new ArgumentNullException(nameof(pointCloudExportService));
        _representativeProfileService = representativeProfileService ?? throw new ArgumentNullException(nameof(representativeProfileService));
        _maximumDropProfileService = maximumDropProfileService;
        _profileAnalysisService = profileAnalysisService;
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

        if (_profileAnalysisService != null ||
            _representativeProfileService is IPointCloudRepresentativeProfilePointExtractor)
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
                    if (_profileAnalysisService != null)
                    {
                        PointCloudProfileAnalysisResult analysisResult =
                            _profileAnalysisService.AnalyzePoints(
                                points,
                                side,
                                archiveContext?.SampleIndex ?? 0);
                        return new PointCloudMedianSectionCaptureResult(
                            string.Empty,
                            analysisResult.ExtractionResult,
                            analysisResult.MaximumDropProfile);
                    }

                    var representativeProfileService =
                        (IPointCloudRepresentativeProfilePointExtractor)_representativeProfileService;
                    MedianSectionExtractionResult onlineExtractionResult =
                        representativeProfileService.ExtractMedianSectionProfileFromPoints(points, side);
                    MaximumDropProfileResult? maximumDropProfile =
                        _maximumDropProfileService?.AnalyzePoints(
                            points,
                            side,
                            archiveContext?.SampleIndex ?? 0);
                    return new PointCloudMedianSectionCaptureResult(
                        string.Empty,
                        onlineExtractionResult,
                        maximumDropProfile);
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

        MedianSectionExtractionResult extractionResult;
        MaximumDropProfileResult? csvMaximumDropProfile;
        if (_profileAnalysisService != null)
        {
            PointCloudProfileAnalysisResult analysisResult = _profileAnalysisService.AnalyzeCsv(
                csvPath,
                side,
                archiveContext?.SampleIndex ?? 0);
            extractionResult = analysisResult.ExtractionResult;
            csvMaximumDropProfile = analysisResult.MaximumDropProfile;
        }
        else
        {
            extractionResult = _representativeProfileService.ExtractMedianSectionProfileFromCsv(
                csvPath,
                side);
            csvMaximumDropProfile = _maximumDropProfileService?.AnalyzeCsv(
                csvPath,
                side,
                archiveContext?.SampleIndex ?? 0);
        }

        return new PointCloudMedianSectionCaptureResult(
            csvPath,
            extractionResult,
            csvMaximumDropProfile);
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
