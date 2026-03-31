using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

/// <summary>
/// 从点云 CSV 中提取代表廓形二维点集，并与二维拟合服务衔接。
/// 输出的 RailProfilePoint 语义为：
/// X -> 轨面横向 Y
/// Y -> 高度 Z
/// </summary>
public sealed class PointCloudRepresentativeProfileService : IPointCloudRepresentativeProfileService
{
    private readonly IRailProfileFittingService _railProfileFittingService;
    const double tolerance = 1e-6;
    public PointCloudRepresentativeProfileService()
        : this(new RailProfileFittingService())
    {
    }

    /// <summary>
    /// 使用外部传入的二维拟合服务构造代表廓形提取服务。
    /// </summary>
    public PointCloudRepresentativeProfileService(IRailProfileFittingService railProfileFittingService)
    {
        _railProfileFittingService = railProfileFittingService ?? throw new ArgumentNullException(nameof(railProfileFittingService));
    }

    /// <summary>
    /// 从点云 CSV 文件中提取中位 X 截面的二维 Y/Z 点集。
    /// </summary>
    public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
    {
        try
        {
            List<PointCloudPoint3D> points = ReadPointsFromCsv(csvPath);
            if (points.Count == 0)
            {
                throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效坐标点。");
            }

            var uniqueXSet = new HashSet<double>();
            foreach (PointCloudPoint3D point in points)
            {
                uniqueXSet.Add(point.X);
            }

            double[] uniqueXValues = uniqueXSet.ToArray();

            if (uniqueXValues.Length == 0)
            {
                throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效的 X 坐标。");
            }

            int medianIndex = (uniqueXValues.Length - 1) / 2;
            double medianX = SelectKthSmallest(uniqueXValues, medianIndex);

            List<RailProfilePoint> sectionPoints = points
                .Where(point => Math.Abs(point.X - medianX) < 1e-6)
                .Select(point => new RailProfilePoint(point.Y, point.Z))
                .ToList();

            if (sectionPoints.Count == 0)
            {
                throw new RepresentativeProfileExtractionException("未找到中位 X 截面的有效 Y/Z 点。");
            }

            return new MedianSectionExtractionResult(medianX, sectionPoints);
        }
        catch (RepresentativeProfileExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException("从 CSV 提取中位 X 截面时发生未处理异常。", ex);
        }
    }

    /// <summary>
    /// 读取点云 CSV，忽略前进方向 X，直接按横向 Y 分箱生成代表廓形二维点集。
    /// </summary>
    public IReadOnlyList<RailProfilePoint> ExtractRepresentativeProfile(
        string csvPath,
        RepresentativeProfileExtractionOptions? options = null)
    {
        RepresentativeProfileExtractionOptions effectiveOptions = ValidateOptions(options ?? new RepresentativeProfileExtractionOptions());

        try
        {
            IReadOnlyList<RailProfilePoint> profile = BuildRepresentativeProfileFromCsv(csvPath, effectiveOptions);
            if (profile.Count < 4)
            {
                throw new RepresentativeProfileExtractionException("代表廓形有效点不足 4 个，无法用于后续拟合。");
            }

            return profile;
        }
        catch (RepresentativeProfileExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException("提取代表廓形时发生未处理异常。", ex);
        }
    }

    /// <summary>
    /// 基于提取出的代表廓形二维点集执行拟合，返回拟合结果。
    /// </summary>
    public RailProfileFitResult FitRepresentativeProfile(
        string csvPath,
        RepresentativeProfileExtractionOptions? options = null)
    {
        IReadOnlyList<RailProfilePoint> profile = ExtractRepresentativeProfile(csvPath, options);
        return _railProfileFittingService.Fit(profile);
    }

    /// <summary>
    /// 校验代表廓形提取参数，避免无效配置进入算法流程。
    /// </summary>
    private static RepresentativeProfileExtractionOptions ValidateOptions(RepresentativeProfileExtractionOptions options)
    {
        if (options.GridStepY <= 0.0)
        {
            throw new RepresentativeProfileExtractionException("GridStepY 必须大于 0。");
        }

        if (options.RailMaxY <= options.RailMinY)
        {
            throw new RepresentativeProfileExtractionException("横向范围无效，RailMaxY 必须大于 RailMinY。");
        }

        if (options.MinSamplesPerYBin < 1)
        {
            throw new RepresentativeProfileExtractionException("MinSamplesPerYBin 不能小于 1。");
        }

        return options;
    }

    /// <summary>
    /// 直接从 CSV 流式聚合代表廓形，避免将全部点云加载到内存中。
    /// </summary>
    private static IReadOnlyList<RailProfilePoint> BuildRepresentativeProfileFromCsv(
        string csvPath,
        RepresentativeProfileExtractionOptions options)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 路径不能为空。");
        }

        if (!File.Exists(csvPath))
        {
            throw new RepresentativeProfileExtractionException($"点云 CSV 文件不存在: {csvPath}");
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
        int yIndex = 1;
        int zIndex = 2;

        if (hasHeader)
        {
            (_, yIndex, zIndex) = ResolveColumnIndexes(firstParts);
        }

        var groupedZValues = new Dictionary<int, List<double>>();
        if (!hasHeader)
        {
            ProcessDataLine(firstNonEmptyLine, delimiter, yIndex, zIndex, options, groupedZValues);
        }

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ProcessDataLine(line, delimiter, yIndex, zIndex, options, groupedZValues);
        }

        if (groupedZValues.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效的 Y/Z 点。");
        }

        return BuildProfileFromBins(groupedZValues, options);
    }

    /// <summary>
    /// 处理一行 CSV 数据，并将其加入对应的横向分箱。
    /// </summary>
    private static void ProcessDataLine(
        string line,
        char delimiter,
        int yIndex,
        int zIndex,
        RepresentativeProfileExtractionOptions options,
        Dictionary<int, List<double>> groupedZValues)
    {
        string[] parts = SplitLine(line, delimiter);
        if (parts.Length <= Math.Max(yIndex, zIndex))
        {
            return;
        }

        if (!TryParseDouble(parts[yIndex], out double y) || !TryParseDouble(parts[zIndex], out double z))
        {
            return;
        }

        if (double.IsNaN(y) || double.IsInfinity(y) || double.IsNaN(z) || double.IsInfinity(z))
        {
            return;
        }

        double minAllowedY = options.RailMinY - options.LateralMargin;
        double maxAllowedY = options.RailMaxY + options.LateralMargin;
        if (y < minAllowedY || y > maxAllowedY)
        {
            return;
        }

        int binIndex = QuantizeToIndex(y, options.RailMinY, options.GridStepY);
        if (!groupedZValues.TryGetValue(binIndex, out List<double>? zValues))
        {
            zValues = new List<double>();
            groupedZValues[binIndex] = zValues;
        }

        zValues.Add(z);
    }

    /// <summary>
    /// 从已聚合的分箱数据中生成最终代表廓形二维点集。
    /// </summary>
    private static IReadOnlyList<RailProfilePoint> BuildProfileFromBins(
        Dictionary<int, List<double>> groupedZValues,
        RepresentativeProfileExtractionOptions options)
    {
        var profile = new List<RailProfilePoint>(groupedZValues.Count);

        foreach (KeyValuePair<int, List<double>> group in groupedZValues.OrderBy(group => group.Key))
        {
            if (group.Value.Count < options.MinSamplesPerYBin)
            {
                continue;
            }

            double y = options.RailMinY + group.Key * options.GridStepY;
            profile.Add(new RailProfilePoint(y, ComputeMedian(group.Value)));
        }

        return profile;
    }

    /// <summary>
    /// 按固定步长将横向坐标映射为整数分箱索引，降低浮点字典键的开销。
    /// </summary>
    private static int QuantizeToIndex(double value, double origin, double step)
    {
        return (int)Math.Round((value - origin) / step, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 计算一组数值的中位数，用于降低飞点对截面和代表廓形的影响。
    /// </summary>
    private static double ComputeMedian(IReadOnlyCollection<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2.0
            : sorted[middle];
    }

    /// <summary>
    /// 根据首行内容推断 CSV 使用的分隔符。
    /// </summary>
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
    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter, StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 读取首个非空行，用于识别分隔符和判断是否存在表头。
    /// </summary>
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
    private static bool LooksLikeHeader(IReadOnlyCollection<string> values)
    {
        return values.Any(value => value.Any(char.IsLetter));
    }

    /// <summary>
    /// 从表头中解析出 X、Y、Z 三列对应的列索引。
    /// </summary>
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
    private static bool TryParseDouble(string value, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result);
    }

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

    private static void Swap(double[] values, int left, int right)
    {
        if (left == right)
        {
            return;
        }

        (values[left], values[right]) = (values[right], values[left]);
    }

    private readonly record struct PointCloudPoint3D(double X, double Y, double Z);
}
