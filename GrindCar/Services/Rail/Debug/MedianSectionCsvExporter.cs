using System;
using System.Collections.Generic;
using System.Globalization;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 代表截面导出器。
/// </summary>
public static class MedianSectionCsvExporter
{
    public static void Export(string outputPath, double representativeY, IReadOnlyList<RailProfilePoint> points)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("导出文件路径不能为空。", nameof(outputPath));
        }

        string metadataLine = $"RepresentativeY,{representativeY.ToString("F6", CultureInfo.InvariantCulture)}";
        RailProfilePointCsvExporter.Export(
            outputPath,
            points,
            "X,Z",
            "导出文件路径不能为空。",
            "当前没有可导出的代表截面数据。",
            "导出文件目录无效。",
            metadataLine);
    }
}
