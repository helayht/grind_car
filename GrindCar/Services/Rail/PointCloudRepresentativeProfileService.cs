using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

/// <summary>
/// 从在线点集或点云 CSV 中提取平均代表廓形二维点集。
/// 带侧别提取时先对原始有效点执行镜像和旋转，再生成平均代表廓形，最后应用平移和裁切。
/// 输出的 RailProfilePoint 语义为：
/// X -> 轨面横向 X
/// Y -> 高度 Z
/// </summary>
public sealed class PointCloudRepresentativeProfileService :
    IPointCloudRepresentativeProfileService,
    IPointCloudRepresentativeProfilePointExtractor
{
    /// <summary>
    /// 数学舍入容差，用于浮点数相等性比较（如判断 X 坐标是否相同）。
    /// </summary>
    private const double Tolerance = 1e-7;

    /// <summary>
    /// Y 方向截面分组容差 (mm)。
    /// 与廓形仪 Y 方向物理分辨率匹配，避免传感器噪声导致同一物理截面被错误拆分为多个伪截面。
    /// 典型廓形仪 Y 分辨率约 0.2~1.0 mm，此处取 0.25 mm（半分辨率）作为默认容差。
    /// </summary>
    private const double DefaultYSplitToleranceMm = 0.25;

    /// <summary>
    /// 最低有效截面数。低于此阈值意味着传感器数据质量异常，不应继续计算。
    /// 实际廓形仪一帧通常产生 10~50 个有效截面，设为 5 可在保证可用性的同时拦截严重退化的数据。
    /// </summary>
    private const int MinValidSectionCount = 5;
    private const int MinOutlierFilteredAverageValueCount = 5;
    private const double MadScaleFactor = 1.4826;
    private const double ZOutlierSigmaFactor = 3.0;
    private const double MinZOutlierThreshold = 0.001;
    private readonly ProfileRegistrationSettingsStore _registrationSettingsStore;
    private readonly ProfileRegistrationTransformService _registrationTransformService;

    public PointCloudRepresentativeProfileService()
        : this(new ProfileRegistrationSettingsStore(), new ProfileRegistrationTransformService())
    {
    }

    internal PointCloudRepresentativeProfileService(
        ProfileRegistrationSettingsStore registrationSettingsStore,
        ProfileRegistrationTransformService? registrationTransformService = null)
    {
        _registrationSettingsStore = registrationSettingsStore ?? throw new ArgumentNullException(nameof(registrationSettingsStore));
        _registrationTransformService = registrationTransformService ?? new ProfileRegistrationTransformService();
    }

    /// <summary>
    /// 从点云 CSV 文件中提取所有有效截面的算术平均二维 X/Z 点集。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <returns>包含代表 Y 值和对应二维点集的提取结果。</returns>
    public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
    {
        try
        {
            List<PointCloudPoint3D> points = PointCloudCsvReader.ReadPointsFromCsv(csvPath);
            return ExtractMedianSectionProfileFromPoints(points);
        }
        catch (RepresentativeProfileExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException("从 CSV 提取平均代表截面时发生未处理异常。", ex);
        }
    }

    public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath, PointCloudDeviceSide side)
    {
        try
        {
            List<PointCloudPoint3D> points = PointCloudCsvReader.ReadPointsFromCsv(csvPath);
            return ExtractMedianSectionProfileFromPoints(points, side);
        }
        catch (RepresentativeProfileExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException("从 CSV 提取并对齐平均代表截面时发生未处理异常。", ex);
        }
    }

    internal MedianSectionExtractionResult ExtractMedianSectionProfileFromPoints(IReadOnlyList<PointCloudPoint3D> points)
    {
        return ExtractMedianSectionProfileCore(points, null, null);
    }

    internal MedianSectionExtractionResult ExtractMedianSectionProfileFromPoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side)
    {
        return ExtractMedianSectionProfileCore(points, side, null);
    }

    internal MedianSectionExtractionResult ExtractMedianSectionProfileFromPoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side,
        ProfileRegistrationParameters parameters)
    {
        return ExtractMedianSectionProfileCore(points, side, parameters);
    }

    MedianSectionExtractionResult IPointCloudRepresentativeProfilePointExtractor.ExtractMedianSectionProfileFromPoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side)
    {
        return ExtractMedianSectionProfileFromPoints(points, side);
    }

    private MedianSectionExtractionResult ExtractMedianSectionProfileCore(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide? side,
        ProfileRegistrationParameters? explicitParameters)
    {
        if (points == null || points.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云数据中未解析到有效坐标点。");
        }

        List<PointCloudPoint3D> validPoints = FilterValidPoints(points);
        if (validPoints.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云数据中未解析到有效坐标点。");
        }

        double representativeY = ResolveAverageY(validPoints);
        ProfileRegistrationParameters? registrationParameters = null;
        IReadOnlyList<PointCloudPoint3D> pointsForExtraction = validPoints;
        if (side.HasValue)
        {
            registrationParameters = explicitParameters ??
                _registrationSettingsStore.LoadRequired().GetParameters(side.Value);
            pointsForExtraction = _registrationTransformService.ApplyRawOrientation(
                validPoints,
                registrationParameters,
                side.Value);
        }

        List<RailProfilePoint> sectionPoints = CreateAverageSectionPoints(pointsForExtraction);
        if (sectionPoints.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("未找到平均代表截面的有效 X/Z 点。");
        }

        List<RailProfilePoint> filteredSectionPoints =
            RepresentativeProfilePointProcessor.FilterOutlierRepresentativePoints(sectionPoints);
        List<RailProfilePoint> profilePoints = filteredSectionPoints;
        if (registrationParameters != null)
        {
            profilePoints = new List<RailProfilePoint>(
                _registrationTransformService.ApplyTranslationAndCrop(
                    filteredSectionPoints,
                    registrationParameters));
        }

        return new MedianSectionExtractionResult(representativeY, profilePoints);
    }

    private static List<PointCloudPoint3D> FilterValidPoints(IReadOnlyList<PointCloudPoint3D> points)
    {
        var validPoints = new List<PointCloudPoint3D>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            PointCloudPoint3D point = points[index];
            if (IsZeroPoint(point))
            {
                continue;
            }

            validPoints.Add(point);
        }

        return validPoints;
    }

    /// <summary>
    /// 计算代表 Y 坐标。
    /// 取所有唯一 Y 值的中位数，避免采样密度不均匀（如中间密、两端疏）导致算术平均向密集区偏移。
    /// </summary>
    /// <param name="points">有效点云点集。</param>
    /// <returns>代表 Y 值。</returns>
    private static double ResolveAverageY(IReadOnlyList<PointCloudPoint3D> points)
    {
        var uniqueYSet = new HashSet<double>();
        for (int index = 0; index < points.Count; index++)
        {
            uniqueYSet.Add(points[index].Y);
        }

        if (uniqueYSet.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效的 Y 坐标。");
        }

        double[] sortedUniqueY = new double[uniqueYSet.Count];
        uniqueYSet.CopyTo(sortedUniqueY);
        Array.Sort(sortedUniqueY);

        int mid = sortedUniqueY.Length / 2;
        if (sortedUniqueY.Length % 2 == 0)
        {
            return (sortedUniqueY[mid - 1] + sortedUniqueY[mid]) / 2.0;
        }

        return sortedUniqueY[mid];
    }

    private static List<RailProfilePoint> CreateAverageSectionPoints(IReadOnlyList<PointCloudPoint3D> points)
    {
        List<List<SectionPoint>> sections = BuildValidSections(points);
        if (sections.Count < MinValidSectionCount)
        {
            throw new RepresentativeProfileExtractionException("有效轮廓数量不足，无法计算平均代表截面。");
        }

        double minCommonX = sections[0][0].X;
        double maxCommonX = sections[0][^1].X;
        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            List<SectionPoint> section = sections[sectionIndex];
            minCommonX = Math.Max(minCommonX, section[0].X);
            maxCommonX = Math.Min(maxCommonX, section[^1].X);
        }

        if (maxCommonX - minCommonX <= Tolerance)
        {
            throw new RepresentativeProfileExtractionException("有效轮廓没有公共 X 范围，无法计算平均代表截面。");
        }

        double gridStep = ResolveTypicalXStep(sections);
        var sectionPoints = new List<RailProfilePoint>();
        int gridIndex = 0;
        while (true)
        {
            double x = minCommonX + gridIndex * gridStep;
            if (x > maxCommonX + Tolerance)
            {
                break;
            }

            if (x > maxCommonX)
            {
                x = maxCommonX;
            }

            if (TryCreateAverageSectionPoint(sections, x, out RailProfilePoint sectionPoint))
            {
                sectionPoints.Add(sectionPoint);
            }

            if (Math.Abs(x - maxCommonX) <= Tolerance)
            {
                break;
            }

            gridIndex++;
        }

        if (sectionPoints.Count == 0 ||
            Math.Abs(sectionPoints[^1].X - maxCommonX) > Tolerance)
        {
            if (TryCreateAverageSectionPoint(sections, maxCommonX, out RailProfilePoint sectionPoint))
            {
                sectionPoints.Add(sectionPoint);
            }
        }

        return sectionPoints;
    }

    private static bool TryCreateAverageSectionPoint(
        IReadOnlyList<List<SectionPoint>> sections,
        double x,
        out RailProfilePoint sectionPoint)
    {
        var zValues = new List<double>(sections.Count);
        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            if (TryInterpolateZ(sections[sectionIndex], x, out double z))
            {
                zValues.Add(z);
            }
        }

        if (zValues.Count == 0)
        {
            sectionPoint = default;
            return false;
        }

        double averageZ = CalculateOutlierFilteredAverage(zValues);
        sectionPoint = new RailProfilePoint(x, averageZ);
        return true;
    }

    private static double CalculateOutlierFilteredAverage(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("平均值输入不能为空。", nameof(values));
        }

        if (values.Count < MinOutlierFilteredAverageValueCount)
        {
            return Average(values);
        }

        double median = Median(values);
        double[] absoluteDeviations = new double[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            absoluteDeviations[index] = Math.Abs(values[index] - median);
        }

        double mad = Median(absoluteDeviations);
        double robustSigma = MadScaleFactor * mad;
        double threshold = Math.Max(MinZOutlierThreshold, ZOutlierSigmaFactor * robustSigma);

        double filteredSum = 0.0;
        int filteredCount = 0;
        for (int index = 0; index < values.Count; index++)
        {
            if (Math.Abs(values[index] - median) <= threshold)
            {
                filteredSum += values[index];
                filteredCount++;
            }
        }

        return filteredCount > 0
            ? filteredSum / filteredCount
            : median;
    }

    private static List<List<SectionPoint>> BuildValidSections(IReadOnlyList<PointCloudPoint3D> points)
    {
        List<List<PointCloudPoint3D>> rawSections = SplitSectionsByY(points);
        var sections = new List<List<SectionPoint>>();
        for (int index = 0; index < rawSections.Count; index++)
        {
            List<SectionPoint> section = NormalizeSection(rawSections[index]);
            if (section.Count >= 2 && section[^1].X - section[0].X > Tolerance)
            {
                sections.Add(section);
            }
        }

        return sections;
    }

    private static List<List<PointCloudPoint3D>> SplitSectionsByY(IReadOnlyList<PointCloudPoint3D> points)
    {
        List<PointCloudPoint3D> sortedPoints = new(points);
        sortedPoints.Sort((left, right) =>
        {
            int yComparison = left.Y.CompareTo(right.Y);
            return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
        });

        var sections = new List<List<PointCloudPoint3D>>();
        List<PointCloudPoint3D>? currentSection = null;
        double currentY = 0.0;
        for (int index = 0; index < sortedPoints.Count; index++)
        {
            PointCloudPoint3D point = sortedPoints[index];
            // 使用与传感器物理分辨率匹配的 Y 方向容差进行截面分组，
            // 避免因传感器微小噪声将同一物理截面拆成多个点数极少的伪截面。
            if (currentSection == null || Math.Abs(point.Y - currentY) > DefaultYSplitToleranceMm)
            {
                currentSection = new List<PointCloudPoint3D>();
                sections.Add(currentSection);
                currentY = point.Y;
            }

            currentSection.Add(point);
        }

        return sections;
    }

    private static List<SectionPoint> NormalizeSection(IReadOnlyList<PointCloudPoint3D> points)
    {
        List<PointCloudPoint3D> sortedPoints = new(points);
        sortedPoints.Sort((left, right) => left.X.CompareTo(right.X));

        var section = new List<SectionPoint>();
        var xAccumulator = new AverageAccumulator();
        var zAccumulator = new AverageAccumulator();
        double currentX = sortedPoints[0].X;
        for (int index = 0; index < sortedPoints.Count; index++)
        {
            PointCloudPoint3D point = sortedPoints[index];
            if (Math.Abs(point.X - currentX) > Tolerance)
            {
                section.Add(new SectionPoint(xAccumulator.Average, zAccumulator.Average));
                xAccumulator = new AverageAccumulator();
                zAccumulator = new AverageAccumulator();
                currentX = point.X;
            }

            xAccumulator.Add(point.X);
            zAccumulator.Add(point.Z);
        }

        section.Add(new SectionPoint(xAccumulator.Average, zAccumulator.Average));
        return section;
    }


    /// <summary>
    /// 计算代表网格步长。
    /// 先对每个截面取相邻 X 间距的中位数，再对所有截面的中位数取中位数。
    /// 两层中位数可有效抵御个别退化截面（如异常稀疏/密集）对全局网格步长的干扰。
    /// </summary>
    /// <param name="sections">所有有效截面。</param>
    /// <returns>代表网格步长。</returns>
    private static double ResolveTypicalXStep(List<List<SectionPoint>> sections)
    {
        var sectionMedianSteps = new List<double>();
        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            List<SectionPoint> section = sections[sectionIndex];
            if (section.Count < 2)
            {
                continue;
            }

            double stepsSum = 0.0;
            int stepsCount = 0;
            for (int index = 1; index < section.Count; index++)
            {
                double interval = section[index].X - section[index - 1].X;
                if (interval > Tolerance)
                {
                    stepsSum += interval;
                    stepsCount++;
                }
            }

            if (stepsCount > 0)
            {
                // 每截面取平均间距作为该截面的代表步长，避免对单截面内采样不均过度敏感
                sectionMedianSteps.Add(stepsSum / stepsCount);
            }
        }

        if (sectionMedianSteps.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("有效轮廓横向采样间距无效，无法计算平均代表截面。");
        }

        // 对所有截面的代表步长取中位数
        double[] values = sectionMedianSteps.ToArray();
        int medianIndex = (values.Length - 1) / 2;
        double step = QuickSelect.SelectKthSmallest(values, medianIndex);
        if (step <= Tolerance)
        {
            throw new RepresentativeProfileExtractionException("有效轮廓横向采样间距无效，无法计算平均代表截面。");
        }

        return step;
    }

    private static bool TryInterpolateZ(IReadOnlyList<SectionPoint> section, double x, out double z)
    {
        z = 0.0;
        if (section.Count == 0 || x < section[0].X - Tolerance || x > section[^1].X + Tolerance)
        {
            return false;
        }

        if (Math.Abs(x - section[0].X) <= Tolerance)
        {
            z = section[0].Z;
            return true;
        }

        if (Math.Abs(x - section[^1].X) <= Tolerance)
        {
            z = section[^1].Z;
            return true;
        }

        int left = 0;
        int right = section.Count - 1;
        while (right - left > 1)
        {
            int middle = left + (right - left) / 2;
            if (section[middle].X <= x)
            {
                left = middle;
            }
            else
            {
                right = middle;
            }
        }

        SectionPoint leftPoint = section[left];
        SectionPoint rightPoint = section[right];
        double span = rightPoint.X - leftPoint.X;
        if (span <= Tolerance)
        {
            return false;
        }

        double ratio = (x - leftPoint.X) / span;
        z = leftPoint.Z + (rightPoint.Z - leftPoint.Z) * ratio;
        return true;
    }

    private static double Average(IReadOnlyList<double> values)
    {
        double sum = 0.0;
        for (int index = 0; index < values.Count; index++)
        {
            sum += values[index];
        }

        return sum / values.Count;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("中位数输入不能为空。", nameof(values));
        }

        int mid = values.Count / 2;
        double[] firstSelectionValues = new double[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            firstSelectionValues[index] = values[index];
        }

        if (values.Count % 2 == 0)
        {
            double[] secondSelectionValues = new double[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                secondSelectionValues[index] = values[index];
            }

            double lowerMedian = QuickSelect.SelectKthSmallest(firstSelectionValues, mid - 1);
            double upperMedian = QuickSelect.SelectKthSmallest(secondSelectionValues, mid);
            return (lowerMedian + upperMedian) / 2.0;
        }

        return QuickSelect.SelectKthSmallest(firstSelectionValues, mid);
    }

    private static bool IsZeroPoint(PointCloudPoint3D point)
    {
        return Math.Abs(point.X) < Tolerance &&
               Math.Abs(point.Y) < Tolerance &&
               Math.Abs(point.Z) < Tolerance;
    }

    private sealed class AverageAccumulator
    {
        private double _sum;
        private int _count;

        public double Average => _sum / _count;

        public void Add(double value)
        {
            _sum += value;
            _count++;
        }
    }

    private readonly record struct SectionPoint(double X, double Z);
}
