using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

/// <summary>
/// 从点云 CSV 中提取代表廓形二维点集。
/// 输出的 RailProfilePoint 语义为：
/// X -> 轨面横向 X
/// Y -> 高度 Z
/// </summary>
public sealed class PointCloudRepresentativeProfileService : IPointCloudRepresentativeProfileService
{
    private const double Tolerance = 1e-7;
    private const double RepresentativeRotationDegrees = 23.0;
    private const double DegreesToRadiansFactor = Math.PI / 180.0;

    /// <summary>
    /// 从点云 CSV 文件中提取中位 Y 截面的二维 X/Z 点集。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <returns>包含中位 Y 值和对应二维点集的提取结果。</returns>
    public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
    {
        try
        {
            List<PointCloudPoint3D> points = ReadPointsFromCsv(csvPath);
            if (points.Count == 0)
            {
                throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效坐标点。");
            }

            var uniqueYSet = new HashSet<double>();
            foreach (PointCloudPoint3D point in points)
            {
                uniqueYSet.Add(point.Y);
            }

            double[] uniqueYValues = uniqueYSet.ToArray();

            if (uniqueYValues.Length == 0)
            {
                throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效的 Y 坐标。");
            }

            int medianIndex = (uniqueYValues.Length - 1) / 2;
            double medianY = SelectKthSmallest(uniqueYValues, medianIndex);

            List<RailProfilePoint> sectionPoints = points
                .Where(point => Math.Abs(point.Y - medianY) < Tolerance)
                .Select(point => new RailProfilePoint(point.X, point.Z))
                .ToList();

            if (sectionPoints.Count == 0)
            {
                throw new RepresentativeProfileExtractionException("未找到中位 Y 截面的有效 X/Z 点。");
            }

            List<RailProfilePoint> rotatedSectionPoints =
                RotateRepresentativePoints(sectionPoints, RepresentativeRotationDegrees);
            return new MedianSectionExtractionResult(medianY, rotatedSectionPoints);
        }
        catch (RepresentativeProfileExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException("从 CSV 提取中位 Y 截面时发生未处理异常。", ex);
        }
    }

    /// <summary>
    /// 根据首行内容推断 CSV 使用的分隔符。
    /// </summary>
    /// <param name="line">CSV 的首个非空行。</param>
    /// <returns>推断得到的分隔符字符。</returns>
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

    /// <summary>
    /// 按指定分隔符拆分一行 CSV 文本，并移除首尾空白。
    /// </summary>
    /// <param name="line">待拆分的文本行。</param>
    /// <param name="delimiter">分隔符。</param>
    /// <returns>拆分后的字段数组。</returns>
    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter, StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 读取首个非空行，用于识别分隔符和判断是否存在表头。
    /// </summary>
    /// <param name="reader">用于读取文件内容的流读取器。</param>
    /// <returns>首个非空行；若文件中没有有效内容则返回 <c>null</c>。</returns>
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

    /// <summary>
    /// 判断 CSV 首行是否更像表头而不是数据行。
    /// </summary>
    /// <param name="values">首行拆分后的字段集合。</param>
    /// <returns>若包含字母字符则认为是表头。</returns>
    private static bool LooksLikeHeader(IReadOnlyCollection<string> values)
    {
        return values.Any(value => value.Any(char.IsLetter));
    }

    /// <summary>
    /// 从表头中解析出 X、Y、Z 三列对应的列索引。
    /// </summary>
    /// <param name="headers">CSV 表头字段集合。</param>
    /// <returns>X、Y、Z 三列对应的索引元组。</returns>
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

    /// <summary>
    /// 在表头集合中查找指定坐标轴对应的列位置。
    /// </summary>
    /// <param name="headers">CSV 表头字段集合。</param>
    /// <param name="axisName">目标坐标轴名称。</param>
    /// <returns>匹配列的索引；未找到时返回 <c>-1</c>。</returns>
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

    /// <summary>
    /// 兼容 InvariantCulture 和当前区域设置解析浮点数。
    /// </summary>
    /// <param name="value">待解析的文本值。</param>
    /// <param name="result">解析成功后的浮点值。</param>
    /// <returns>若解析成功则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    private static bool TryParseDouble(string value, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result);
    }

    /// <summary>
    /// 从点云 CSV 文件中读取有效三维点集合。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <returns>解析成功的三维点列表。</returns>
    private static List<PointCloudPoint3D> ReadPointsFromCsv(string csvPath)
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

    /// <summary>
    /// 尝试从一行 CSV 字段中读取一个有效三维点。
    /// </summary>
    /// <param name="parts">一行 CSV 拆分后的字段数组。</param>
    /// <param name="xIndex">X 列索引。</param>
    /// <param name="yIndex">Y 列索引。</param>
    /// <param name="zIndex">Z 列索引。</param>
    /// <param name="point">读取成功后的三维点。</param>
    /// <returns>若成功读取则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
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

    /// <summary>
    /// 使用快速选择算法返回数组中第 k 小的值。
    /// </summary>
    /// <param name="values">待选择的数组。</param>
    /// <param name="k">目标次序位置，从 0 开始。</param>
    /// <returns>第 k 小的值。</returns>
    private static double SelectKthSmallest(double[] values, int k)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Length == 0)
        {
            throw new ArgumentException("待选择数组不能为空。", nameof(values));
        }

        if (k < 0 || k >= values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        int left = 0;
        int right = values.Length - 1;

        while (true)
        {
            if (left == right)
            {
                return values[left];
            }

            int pivotIndex = left + (right - left) / 2;
            (int equalStart, int equalEnd) = Partition(values, left, right, pivotIndex);
            if (k < equalStart)
            {
                right = equalStart - 1;
                continue;
            }

            if (k > equalEnd)
            {
                left = equalEnd + 1;
                continue;
            }

            return values[k];
        }
    }

    /// <summary>
    /// 对数组指定区间执行三路分区。
    /// </summary>
    /// <param name="values">待分区数组。</param>
    /// <param name="left">区间左边界。</param>
    /// <param name="right">区间右边界。</param>
    /// <param name="pivotIndex">枢轴索引。</param>
    /// <returns>等于枢轴值区间的起止索引。</returns>
    private static (int equalStart, int equalEnd) Partition(double[] values, int left, int right, int pivotIndex)
    {
        double pivotValue = values[pivotIndex];
        Swap(values, pivotIndex, right);

        int less = left;
        int current = left;
        int greater = right;

        while (current <= greater)
        {
            if (values[current] < pivotValue)
            {
                Swap(values, less, current);
                less++;
                current++;
            }
            else if (values[current] > pivotValue)
            {
                Swap(values, current, greater);
                greater--;
            }
            else
            {
                current++;
            }
        }

        return (less, greater);
    }

    /// <summary>
    /// 交换数组中两个位置的元素。
    /// </summary>
    /// <param name="values">目标数组。</param>
    /// <param name="left">左侧索引。</param>
    /// <param name="right">右侧索引。</param>
    private static void Swap(double[] values, int left, int right)
    {
        if (left == right)
        {
            return;
        }

        (values[left], values[right]) = (values[right], values[left]);
    }

    /// <summary>
    /// 将代表截面点集按指定角度围绕原点进行逆时针旋转。
    /// </summary>
    /// <param name="points">待旋转的代表点集。</param>
    /// <param name="angleDegrees">旋转角度（度）。</param>
    /// <returns>旋转后的点集。</returns>
    private static List<RailProfilePoint> RotateRepresentativePoints(
        IReadOnlyList<RailProfilePoint> points,
        double angleDegrees)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        double radians = angleDegrees * DegreesToRadiansFactor;
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var rotatedPoints = new List<RailProfilePoint>(points.Count);

        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            double rotatedX = point.X * cosValue - point.Y * sinValue;
            double rotatedY = point.X * sinValue + point.Y * cosValue;
            rotatedPoints.Add(new RailProfilePoint(rotatedX, rotatedY));
        }

        return rotatedPoints;
    }

    /// <summary>
    /// 表示从点云 CSV 中读取出的三维点。
    /// </summary>
    /// <param name="X">横向坐标。</param>
    /// <param name="Y">前进方向坐标。</param>
    /// <param name="Z">高度坐标。</param>
    private readonly record struct PointCloudPoint3D(double X, double Y, double Z);
}
