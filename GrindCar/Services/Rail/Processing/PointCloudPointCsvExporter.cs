using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 原始三维点云 CSV 导出器。
/// </summary>
internal static class PointCloudPointCsvExporter
{
    public static void Export(string outputPath, IReadOnlyList<PointCloudPoint3D> points)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("点云留档路径不能为空。");
        }

        if (points == null || points.Count == 0)
        {
            throw new InvalidOperationException("点云点集不能为空。");
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("点云留档目录无效。");
        }

        Directory.CreateDirectory(directoryPath);

        var builder = new StringBuilder();
        builder.AppendLine("X,Y,Z");
        for (int index = 0; index < points.Count; index++)
        {
            PointCloudPoint3D point = points[index];
            builder.Append(point.X.ToString("F6", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(point.Y.ToString("F6", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(point.Z.ToString("F6", CultureInfo.InvariantCulture));
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
