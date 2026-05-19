using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

        if (points == null || points.Count == 0)
        {
            throw new InvalidOperationException("当前没有可导出的代表截面数据。");
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("导出文件目录无效。");
        }

        Directory.CreateDirectory(directoryPath);

        var builder = new StringBuilder();
        builder.AppendLine($"RepresentativeY,{representativeY.ToString("F6", CultureInfo.InvariantCulture)}");
        builder.AppendLine("X,Z");

        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            builder.Append(point.X.ToString("F6", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(point.Y.ToString("F6", CultureInfo.InvariantCulture));
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
