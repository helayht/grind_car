using System;
using System.Globalization;
using System.IO;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;

namespace GrindCar.Services.Rail;

/// <summary>
/// 将 SDK 点云采集与中位 Y 截面提取串联起来。
/// 当前采用“先导出 CSV 到 Log 目录，再读取 CSV 提取截面”的方式。
/// </summary>
public sealed class PointCloudMedianSectionCaptureService : IPointCloudMedianSectionCaptureService
{
    private const string DefaultLogDirectoryName = "Log";
    private const string CsvFilePrefix = "point-cloud";
    private readonly PointCloudExportService _pointCloudExportService;
    private readonly IPointCloudRepresentativeProfileService _representativeProfileService;
    private readonly string _logDirectoryPath;

    /// <summary>
    /// 使用默认日志目录和默认服务依赖初始化实例。
    /// </summary>
    public PointCloudMedianSectionCaptureService()
        : this(
            new PointCloudExportService(),
            new PointCloudRepresentativeProfileService(),
            Path.Combine(Environment.CurrentDirectory, DefaultLogDirectoryName))
    {
    }

    /// <summary>
    /// 使用指定依赖和日志目录初始化实例。
    /// </summary>
    /// <param name="pointCloudExportService">点云导出服务。</param>
    /// <param name="representativeProfileService">中位截面提取服务。</param>
    /// <param name="logDirectoryPath">CSV 落盘目录。</param>
    public PointCloudMedianSectionCaptureService(
        PointCloudExportService pointCloudExportService,
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
    /// 采集指定设备的单帧点云并提取中位截面点集。
    /// </summary>
    /// <param name="serialNumber">目标设备序列号。</param>
    /// <returns>包含落盘 CSV 路径和提取结果的对象。</returns>
    public PointCloudMedianSectionCaptureResult CaptureMedianSectionProfile(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new PointCloudSdkException("未选择设备序列号。");
        }

        Directory.CreateDirectory(_logDirectoryPath);
        string csvPath = BuildCsvPath();

        _pointCloudExportService.ExportPointCloud(serialNumber, csvPath, PointCloudExportFormat.Csv);

        MedianSectionExtractionResult extractionResult =
            _representativeProfileService.ExtractMedianSectionProfileFromCsv(csvPath);

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
