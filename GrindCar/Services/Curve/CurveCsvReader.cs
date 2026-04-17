using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace GrindCar.Services.Curve;

/// <summary>
/// 曲线 CSV 读取。
/// </summary>
public static class CurveCsvReader
{
    public static List<Point> ReadPointsFromCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("CSV 路径不能为空。");
        }

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"CSV 文件不存在: {filePath}");
        }

        using var reader = new StreamReader(filePath);
        string? firstLine = ReadFirstNonEmptyLine(reader);
        if (firstLine == null)
        {
            throw new InvalidOperationException("CSV 文件为空。");
        }

        char delimiter = DetectDelimiter(firstLine);
        string[] firstValues = SplitLine(firstLine, delimiter);
        bool hasHeader = firstValues.Any(value => value.Any(char.IsLetter));
        (int xIndex, int yIndex) = hasHeader ? ResolveCoordinateIndexes(firstValues) : (0, 1);

        var points = new List<Point>();
        if (!hasHeader && TryReadPoint(firstValues, xIndex, yIndex, out Point firstPoint))
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

            string[] values = SplitLine(line, delimiter);
            if (TryReadPoint(values, xIndex, yIndex, out Point point))
            {
                points.Add(point);
            }
        }

        return points;
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

    private static (int xIndex, int yIndex) ResolveCoordinateIndexes(IReadOnlyList<string> headers)
    {
        int xIndex = -1;
        int yIndex = -1;

        for (int index = 0; index < headers.Count; index++)
        {
            string normalized = headers[index].Trim().ToLowerInvariant();
            if (xIndex < 0 && (normalized == "x" || normalized.EndsWith("x")))
            {
                xIndex = index;
                continue;
            }

            if (yIndex < 0 && (normalized == "y" || normalized.EndsWith("y")))
            {
                yIndex = index;
            }
        }

        if (xIndex < 0 || yIndex < 0)
        {
            throw new InvalidOperationException("CSV 表头未找到 x/y 列。");
        }

        return (xIndex, yIndex);
    }

    private static bool TryReadPoint(string[] values, int xIndex, int yIndex, out Point point)
    {
        point = default;
        int maxIndex = Math.Max(xIndex, yIndex);
        if (values.Length <= maxIndex)
        {
            return false;
        }

        if (!TryParseDouble(values[xIndex], out double x) ||
            !TryParseDouble(values[yIndex], out double y))
        {
            return false;
        }

        if (double.IsNaN(x) || double.IsInfinity(x) ||
            double.IsNaN(y) || double.IsInfinity(y))
        {
            return false;
        }

        point = new Point(x, y);
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