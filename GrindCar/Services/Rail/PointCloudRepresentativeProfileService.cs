using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Processing;

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
    private const int MinValidSectionCount = 2;

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
        return ExtractMedianSectionProfileCore(points, null);
    }

    internal MedianSectionExtractionResult ExtractMedianSectionProfileFromPoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side)
    {
        return ExtractMedianSectionProfileCore(points, side);
    }

    private static MedianSectionExtractionResult ExtractMedianSectionProfileCore(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide? side)
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
        List<RailProfilePoint> sectionPoints = CreateAverageSectionPoints(validPoints);
        if (sectionPoints.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("未找到平均代表截面的有效 X/Z 点。");
        }

        List<RailProfilePoint> filteredSectionPoints =
            RepresentativeProfilePointProcessor.FilterOutlierRepresentativePoints(sectionPoints);
        List<RailProfilePoint> profilePoints = side.HasValue
            ? RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(filteredSectionPoints, side.Value)
            : filteredSectionPoints;

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

    private static double ResolveAverageY(IReadOnlyList<PointCloudPoint3D> points)
    {
        var uniqueYSet = new HashSet<double>();
        double sumY = 0.0;
        for (int index = 0; index < points.Count; index++)
        {
            double y = points[index].Y;
            if (uniqueYSet.Add(y))
            {
                sumY += y;
            }
        }

        if (uniqueYSet.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效的 Y 坐标。");
        }

        return sumY / uniqueYSet.Count;
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
        var xIntervals = new List<double>();
        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            List<SectionPoint> section = sections[sectionIndex];
            minCommonX = Math.Max(minCommonX, section[0].X);
            maxCommonX = Math.Min(maxCommonX, section[^1].X);
            AddAdjacentXIntervals(section, xIntervals);
        }

        if (maxCommonX - minCommonX <= Tolerance)
        {
            throw new RepresentativeProfileExtractionException("有效轮廓没有公共 X 范围，无法计算平均代表截面。");
        }

        double gridStep = ResolveTypicalXStep(xIntervals);
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

            double sumZ = 0.0;
            int interpolatedCount = 0;
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                if (TryInterpolateZ(sections[sectionIndex], x, out double z))
                {
                    sumZ += z;
                    interpolatedCount++;
                }
            }

            if (interpolatedCount > 0)
            {
                sectionPoints.Add(new RailProfilePoint(x, sumZ / interpolatedCount));
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
            double sumZ = 0.0;
            int interpolatedCount = 0;
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                if (TryInterpolateZ(sections[sectionIndex], maxCommonX, out double z))
                {
                    sumZ += z;
                    interpolatedCount++;
                }
            }

            if (interpolatedCount > 0)
            {
                sectionPoints.Add(new RailProfilePoint(maxCommonX, sumZ / interpolatedCount));
            }
        }

        return sectionPoints;
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
            if (currentSection == null || Math.Abs(point.Y - currentY) > Tolerance)
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

    private static void AddAdjacentXIntervals(IReadOnlyList<SectionPoint> section, ICollection<double> intervals)
    {
        for (int index = 1; index < section.Count; index++)
        {
            double interval = section[index].X - section[index - 1].X;
            if (interval > Tolerance)
            {
                intervals.Add(interval);
            }
        }
    }

    private static double ResolveTypicalXStep(IReadOnlyList<double> xIntervals)
    {
        if (xIntervals.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("有效轮廓横向采样间距无效，无法计算平均代表截面。");
        }

        double[] values = new double[xIntervals.Count];
        for (int index = 0; index < xIntervals.Count; index++)
        {
            values[index] = xIntervals[index];
        }

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
