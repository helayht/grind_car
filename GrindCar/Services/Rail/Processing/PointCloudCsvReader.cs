using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 点云 CSV 解析器。
/// </summary>
internal static class PointCloudCsvReader
{
    public static List<PointCloudPoint3D> ReadPointsFromCsv(string csvPath)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 路径不能为空。");
        }

        if (!File.Exists(csvPath))
        {
            throw new RepresentativeProfileExtractionException($"点云 CSV 文件不存在: {csvPath}");
        }

        if (!string.Equals(Path.GetExtension(csvPath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new RepresentativeProfileExtractionException("当前仅支持 .csv 点云文件。");
        }

        using var reader = new StreamReader(csvPath);
        string? firstNonEmptyLine = ReadFirstNonEmptyLine(reader);
        if (firstNonEmptyLine == null)
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 文件为空。");
        }

        char delimiter = DetectDelimiter(firstNonEmptyLine);
        string[] firstParts = SplitLine(firstNonEmptyLine, delimiter);
        bool hasHeader = LooksLikeHeader(firstParts);
        (int xIndex, int yIndex, int zIndex) = hasHeader
            ? ResolveColumnIndexes(firstParts)
            : (0, 1, 2);

        var points = new List<PointCloudPoint3D>();
        if (!hasHeader && TryReadPoint(firstParts, xIndex, yIndex, zIndex, out PointCloudPoint3D firstPoint))
        {
            points.Add(firstPoint);
        }

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = SplitLine(line, delimiter);
            if (TryReadPoint(parts, xIndex, yIndex, zIndex, out PointCloudPoint3D point))
            {
                points.Add(point);
            }
        }

        return points;
    }

    private static char DetectDelimiter(string line)
    {
        if (line.Contains('\t'))
        {
            return '\t';
        }

        if (line.Contains(';'))
        {
            return ';';
        }

        return ',';
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter, StringSplitOptions.TrimEntries);
    }

    private static string? ReadFirstNonEmptyLine(StreamReader reader)
    {
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return null;
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            for (int charIndex = 0; charIndex < value.Length; charIndex++)
            {
                if (char.IsLetter(value[charIndex]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (int xIndex, int yIndex, int zIndex) ResolveColumnIndexes(IReadOnlyList<string> headers)
    {
        int xIndex = FindAxisIndex(headers, "x");
        int yIndex = FindAxisIndex(headers, "y");
        int zIndex = FindAxisIndex(headers, "z");

        if (xIndex < 0 || yIndex < 0 || zIndex < 0)
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 表头中未找到 X/Y/Z 列。");
        }

        return (xIndex, yIndex, zIndex);
    }

    private static int FindAxisIndex(IReadOnlyList<string> headers, string axisName)
    {
        for (int index = 0; index < headers.Count; index++)
        {
            string normalized = headers[index].Trim().ToLowerInvariant();
            if (normalized == axisName || normalized.EndsWith(axisName))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadPoint(string[] parts, int xIndex, int yIndex, int zIndex, out PointCloudPoint3D point)
    {
        point = default;

        if (parts.Length <= Math.Max(xIndex, Math.Max(yIndex, zIndex)))
        {
            return false;
        }

        if (!TryParseDouble(parts[xIndex], out double x) ||
            !TryParseDouble(parts[yIndex], out double y) ||
            !TryParseDouble(parts[zIndex], out double z))
        {
            return false;
        }

        if (double.IsNaN(x) || double.IsInfinity(x) ||
            double.IsNaN(y) || double.IsInfinity(y) ||
            double.IsNaN(z) || double.IsInfinity(z))
        {
            return false;
        }

        point = new PointCloudPoint3D(x, y, z);
        return true;
    }

    private static bool TryParseDouble(string value, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result);
    }
}