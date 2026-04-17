using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 导出代表点 CSV。
/// </summary>
public static class RepresentativePointsCsvExporter
{
    public static void Export(string outputPath, IReadOnlyList<RailProfilePoint> points)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("导出路径不能为空。");
        }

        if (points == null || points.Count == 0)
        {
            throw new InvalidOperationException("当前没有可导出的代表点，请先执行“计算需要打磨深度”。");
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("导出路径无效。");
        }

        Directory.CreateDirectory(directoryPath);

        var builder = new StringBuilder();
        builder.AppendLine("X,Y");
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