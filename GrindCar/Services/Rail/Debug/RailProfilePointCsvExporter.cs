using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

internal static class RailProfilePointCsvExporter
{
    public static void Export(
        string outputPath,
        IReadOnlyList<RailProfilePoint> points,
        string header,
        string emptyPathMessage,
        string emptyPointsMessage,
        string invalidDirectoryMessage,
        string? metadataLine = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException(emptyPathMessage);
        }

        if (points == null || points.Count == 0)
        {
            throw new InvalidOperationException(emptyPointsMessage);
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException(invalidDirectoryMessage);
        }

        Directory.CreateDirectory(directoryPath);

        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(metadataLine))
        {
            builder.AppendLine(metadataLine);
        }

        builder.AppendLine(header);
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
