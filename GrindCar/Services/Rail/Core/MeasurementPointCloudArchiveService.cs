using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 测量流程原始点云后台留档服务。
/// </summary>
internal static class MeasurementPointCloudArchiveService
{
    private const string LogDirectoryName = "Log";
    private const string ArchiveDirectoryName = "MeasurementPointCloud";
    private const string FilePrefix = "measurement-point-cloud";

    public static void QueueArchive(
        MeasurementPointCloudArchiveContext? context,
        IReadOnlyList<PointCloudPoint3D> points)
    {
        if (context == null)
        {
            return;
        }

        string outputPath = BuildArchiveFilePath(context);
        _ = Task.Run(() =>
        {
            try
            {
                PointCloudPointCsvExporter.Export(outputPath, points);
                context.Progress?.Report($"点云留档完成：{outputPath}");
            }
            catch (Exception ex)
            {
                context.Progress?.Report($"点云留档失败：{ex.Message}");
            }
        });
    }

    internal static string BuildArchiveFilePath(MeasurementPointCloudArchiveContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        string directoryPath = Path.Combine(Environment.CurrentDirectory, LogDirectoryName, ArchiveDirectoryName);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string sampleText = FormatIndex("sample", context.SampleIndex);
        string deviceText = FormatIndex("device", context.DeviceIndex);
        string sideText = context.Side.ToString().ToLowerInvariant();
        string serialNumberText = SanitizeFileNamePart(context.SerialNumber);
        string fileName = $"{FilePrefix}-{timestamp}-{sampleText}-{deviceText}-{sideText}-{serialNumberText}.csv";
        return Path.Combine(directoryPath, fileName);
    }

    private static string FormatIndex(string prefix, int index)
    {
        int normalizedIndex = Math.Max(index, 0);
        return $"{prefix}{normalizedIndex.ToString("D3", CultureInfo.InvariantCulture)}";
    }

    private static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] chars = value.Trim().ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            if (Array.IndexOf(invalidChars, chars[index]) >= 0)
            {
                chars[index] = '_';
            }
        }

        return new string(chars);
    }
}
