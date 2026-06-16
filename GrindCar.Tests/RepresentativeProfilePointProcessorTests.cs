using System;
using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class RepresentativeProfilePointProcessorTests
{
    private const double LeftBoundarySourceX = 100.0;
    private const double RightBoundarySourceX = 200.0;

    [Fact]
    public void MirrorRepresentativePointsAcrossSideAxis_WithLeftSide_MirrorsAcrossMaxX()
    {
        var points = new List<RailProfilePoint>
        {
            new(1.0, 10.0),
            new(3.0, 20.0),
            new(5.0, 30.0)
        };

        List<RailProfilePoint> mirroredPoints =
            RepresentativeProfilePointProcessor.MirrorRepresentativePointsAcrossSideAxis(points, PointCloudDeviceSide.Left);

        const double axisX = 5.0;
        Assert.Equal(points.Count, mirroredPoints.Count);
        for (int index = 0; index < points.Count; index++)
        {
            Assert.Equal(2.0 * axisX, points[index].X + mirroredPoints[index].X, 6);
            Assert.Equal(points[index].Y, mirroredPoints[index].Y, 6);
        }
    }

    [Fact]
    public void MirrorRepresentativePointsAcrossSideAxis_WithRightSide_MirrorsAcrossMinX()
    {
        var points = new List<RailProfilePoint>
        {
            new(2.0, 10.0),
            new(4.0, 20.0),
            new(8.0, 30.0)
        };

        List<RailProfilePoint> mirroredPoints =
            RepresentativeProfilePointProcessor.MirrorRepresentativePointsAcrossSideAxis(points, PointCloudDeviceSide.Right);

        const double axisX = 2.0;
        Assert.Equal(points.Count, mirroredPoints.Count);
        for (int index = 0; index < points.Count; index++)
        {
            Assert.Equal(2.0 * axisX, points[index].X + mirroredPoints[index].X, 6);
            Assert.Equal(points[index].Y, mirroredPoints[index].Y, 6);
        }
    }

    [Fact]
    public void MirrorRepresentativePointsAcrossSideAxis_WithEmptyPoints_ReturnsEmptyList()
    {
        List<RailProfilePoint> mirroredPoints =
            RepresentativeProfilePointProcessor.MirrorRepresentativePointsAcrossSideAxis(
                Array.Empty<RailProfilePoint>(),
                PointCloudDeviceSide.Left);

        Assert.Empty(mirroredPoints);
    }

    [Fact]
    public void MirrorRepresentativePointsAcrossSideAxis_WithNullPoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RepresentativeProfilePointProcessor.MirrorRepresentativePointsAcrossSideAxis(null!, PointCloudDeviceSide.Left));
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithLeftSide_KeepsWorkSurfaceAboveStandard()
    {
        List<RailProfilePoint> points = BuildLeftHalfProfilePoints();

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left);

        double boundaryMedianX = Median(alignedPoints
            .Where(point => Math.Abs(point.X - StandardRailProfileSolver.LeftBoundaryX) < 1e-6)
            .Select(point => point.X)
            .ToArray());

        Assert.Equal(StandardRailProfileSolver.LeftBoundaryX, boundaryMedianX, 6);
        AssertWorkSurfaceIsJustAboveStandard(alignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithRightSide_KeepsWorkSurfaceAboveStandard()
    {
        List<RailProfilePoint> points = BuildRightHalfProfilePoints();

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Right);

        double boundaryMedianX = Median(alignedPoints
            .Where(point => Math.Abs(point.X - StandardRailProfileSolver.RightBoundaryX) < 1e-6)
            .Select(point => point.X)
            .ToArray());

        Assert.Equal(StandardRailProfileSolver.RightBoundaryX, boundaryMedianX, 6);
        AssertWorkSurfaceIsJustAboveStandard(alignedPoints, point => point.X < StandardRailProfileSolver.RightBoundaryX - 1.0);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithBoundaryOutlier_KeepsWorkSurfaceAlignmentStable()
    {
        List<RailProfilePoint> points = BuildLeftHalfProfilePoints();
        points.Add(new RailProfilePoint(97.0, -20.0));

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left);

        double boundaryMedianX = Median(alignedPoints
            .Where(point => Math.Abs(point.X - StandardRailProfileSolver.LeftBoundaryX) < 0.25)
            .Select(point => point.X)
            .ToArray());

        Assert.InRange(boundaryMedianX, StandardRailProfileSolver.LeftBoundaryX - 0.25, StandardRailProfileSolver.LeftBoundaryX + 0.25);
        AssertWorkSurfaceIsJustAboveStandard(alignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithDifferentBoundaryTopHeights_ProducesSameWorkSurfaceGap()
    {
        List<RailProfilePoint> lowBoundaryPoints = BuildLeftHalfProfilePoints(boundaryTopY: 9.0);
        List<RailProfilePoint> highBoundaryPoints = BuildLeftHalfProfilePoints(boundaryTopY: 80.0);

        List<RailProfilePoint> lowAlignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(lowBoundaryPoints, PointCloudDeviceSide.Left);
        List<RailProfilePoint> highAlignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(highBoundaryPoints, PointCloudDeviceSide.Left);

        double lowMinGap = ResolveRobustLowerStandardGap(lowAlignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);
        double highMinGap = ResolveRobustLowerStandardGap(highAlignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);

        Assert.Equal(0.0, lowMinGap, 6);
        Assert.Equal(0.0, highMinGap, 6);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithRejectedBoundaryPoint_DoesNotUseItForVerticalAlignment()
    {
        List<RailProfilePoint> points = BuildLeftHalfProfilePoints();
        points.Add(new RailProfilePoint(100.15, -500.0));

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left);

        double minGap = ResolveRobustLowerStandardGap(
            alignedPoints,
            point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);

        Assert.Equal(0.0, minGap, 6);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithWorkSurfaceLowOutlier_KeepsMainSurfaceAlignmentStable()
    {
        List<RailProfilePoint> points = BuildLeftHalfProfilePoints();
        points.Add(new RailProfilePoint(135.0, -500.0));

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left);

        double robustLowerGap = ResolveRobustLowerStandardGap(
            alignedPoints,
            point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);
        double minGap = ResolveMinimumStandardGap(
            alignedPoints,
            point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);

        Assert.Equal(0.0, robustLowerGap, 6);
        Assert.True(minGap < -100.0, "异常低点应被排除出主体贴合基准。");
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithLeftSideTiltedWithinLimit_RotatesBoundaryToStandard()
    {
        List<RailProfilePoint> points = RotatePoints(
            BuildLeftHalfProfilePoints(),
            25.0,
            new RailProfilePoint(LeftBoundarySourceX, 4.5));

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left);

        double boundaryMedianX = Median(alignedPoints
            .OrderBy(point => point.X)
            .Take(10)
            .Select(point => point.X)
            .ToArray());

        Assert.Equal(StandardRailProfileSolver.LeftBoundaryX, boundaryMedianX, 6);
        AssertWorkSurfaceIsJustAboveStandard(alignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithRightSideTiltedWithinLimit_RotatesBoundaryToStandard()
    {
        List<RailProfilePoint> points = RotatePoints(
            BuildRightHalfProfilePoints(),
            -25.0,
            new RailProfilePoint(RightBoundarySourceX, 4.5));

        List<RailProfilePoint> alignedPoints =
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Right);

        double boundaryMedianX = Median(alignedPoints
            .OrderByDescending(point => point.X)
            .Take(10)
            .Select(point => point.X)
            .ToArray());

        Assert.Equal(StandardRailProfileSolver.RightBoundaryX, boundaryMedianX, 6);
        AssertWorkSurfaceIsJustAboveStandard(alignedPoints, point => point.X < StandardRailProfileSolver.RightBoundaryX - 1.0);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithBoundaryTiltedBeyondLimit_Throws()
    {
        List<RailProfilePoint> points = BuildLeftHalfProfilePointsWithBoundaryAngle(65.0);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left));

        Assert.Contains("超过可修正范围", exception.Message);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithRejectedCandidates_ReportsFailureDetails()
    {
        List<RailProfilePoint> points = BuildLeftHalfProfilePointsOutsideStandardDomain();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left));

        Assert.Contains("旋转对齐失败", exception.Message);
        Assert.Contains("候选失败详情", exception.Message);
        Assert.Contains("Side=Left", exception.Message);
        Assert.Contains("候选角度", exception.Message);
    }

    [Fact]
    public void AlignRepresentativePointsToStandardBoundary_WithLessThanTenPoints_Throws()
    {
        var points = new List<RailProfilePoint>();
        for (int index = 0; index < 9; index++)
        {
            points.Add(new RailProfilePoint(LeftBoundarySourceX, index));
        }

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left));

        Assert.Contains("至少需要 10 个点", exception.Message);
    }

    private static List<RailProfilePoint> BuildLeftHalfProfilePoints(double boundaryTopY = 9.0)
    {
        var points = new List<RailProfilePoint>();
        for (int index = 0; index < 10; index++)
        {
            points.Add(new RailProfilePoint(LeftBoundarySourceX, boundaryTopY * index / 9.0));
        }

        for (int index = 0; index < 40; index++)
        {
            double x = 110.0 + index;
            points.Add(new RailProfilePoint(x, 9.0 + Math.Sqrt(index + 1)));
        }

        return points;
    }

    private static List<RailProfilePoint> BuildLeftHalfProfilePointsOutsideStandardDomain()
    {
        var points = new List<RailProfilePoint>();
        for (int index = 0; index < 10; index++)
        {
            points.Add(new RailProfilePoint(LeftBoundarySourceX, index));
        }

        for (int index = 0; index < 40; index++)
        {
            double x = 1000.0 + index;
            points.Add(new RailProfilePoint(x, 10.0 + index * 0.1));
        }

        return points;
    }

    private static List<RailProfilePoint> BuildLeftHalfProfilePointsWithBoundaryAngle(double angleFromVerticalDegrees)
    {
        double tangent = Math.Tan(angleFromVerticalDegrees * Math.PI / 180.0);
        var points = new List<RailProfilePoint>();
        for (int index = 0; index < 10; index++)
        {
            double y = index;
            points.Add(new RailProfilePoint(LeftBoundarySourceX + y * tangent, y));
        }

        for (int index = 0; index < 40; index++)
        {
            double x = 130.0 + index;
            points.Add(new RailProfilePoint(x, 9.0 + Math.Sqrt(index + 1)));
        }

        return points;
    }

    private static List<RailProfilePoint> BuildRightHalfProfilePoints()
    {
        var points = new List<RailProfilePoint>();
        for (int index = 0; index < 40; index++)
        {
            double x = 110.0 + index;
            points.Add(new RailProfilePoint(x, 9.0 + Math.Sqrt(index + 1)));
        }

        for (int index = 0; index < 10; index++)
        {
            points.Add(new RailProfilePoint(RightBoundarySourceX, index));
        }

        return points;
    }

    private static List<RailProfilePoint> RotatePoints(
        IReadOnlyList<RailProfilePoint> points,
        double degrees,
        RailProfilePoint center)
    {
        double radians = degrees * Math.PI / 180.0;
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var rotatedPoints = new List<RailProfilePoint>(points.Count);

        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            double translatedX = point.X - center.X;
            double translatedY = point.Y - center.Y;
            double rotatedX = translatedX * cosValue - translatedY * sinValue + center.X;
            double rotatedY = translatedX * sinValue + translatedY * cosValue + center.Y;
            rotatedPoints.Add(new RailProfilePoint(rotatedX, rotatedY));
        }

        return rotatedPoints;
    }

    private static void AssertWorkSurfaceIsJustAboveStandard(
        IReadOnlyList<RailProfilePoint> points,
        Func<RailProfilePoint, bool> workSurfacePredicate)
    {
        double robustLowerGap = ResolveRobustLowerStandardGap(points, workSurfacePredicate);
        Assert.Equal(0.0, robustLowerGap, 6);
    }

    private static double ResolveMinimumStandardGap(
        IReadOnlyList<RailProfilePoint> points,
        Func<RailProfilePoint, bool> workSurfacePredicate)
    {
        double minGap = double.PositiveInfinity;
        foreach (RailProfilePoint point in points.Where(workSurfacePredicate))
        {
            double standardY = StandardRailProfileSolver.RailSurfaceFun(point.X);
            if (double.IsNaN(standardY) || double.IsInfinity(standardY))
            {
                continue;
            }

            minGap = Math.Min(minGap, point.Y - standardY);
        }

        Assert.False(double.IsPositiveInfinity(minGap), "未找到有效工作面点。");
        return minGap;
    }

    private static double ResolveRobustLowerStandardGap(
        IReadOnlyList<RailProfilePoint> points,
        Func<RailProfilePoint, bool> workSurfacePredicate)
    {
        double[] gaps = points
            .Where(workSurfacePredicate)
            .Select(point => point.Y - StandardRailProfileSolver.RailSurfaceFun(point.X))
            .Where(gap => !double.IsNaN(gap) && !double.IsInfinity(gap))
            .ToArray();

        Assert.NotEmpty(gaps);
        double[] filteredGaps = FilterDiffOutliersByMad(gaps).ToArray();
        double[] sourceGaps = filteredGaps.Length > 0 ? filteredGaps : gaps;
        return Percentile(sourceGaps, 0.05);
    }

    private static IReadOnlyList<double> FilterDiffOutliersByMad(IReadOnlyList<double> diffs)
    {
        if (diffs.Count < 5)
        {
            return diffs;
        }

        double median = Median(diffs);
        double[] centeredAbsoluteDiffs = diffs
            .Select(diff => Math.Abs(diff - median))
            .ToArray();
        double mad = Median(centeredAbsoluteDiffs);
        double threshold = Math.Max(0.001, 3.0 * 1.4826 * mad);
        return diffs
            .Where(diff => Math.Abs(diff - median) <= threshold)
            .ToArray();
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        double position = (sorted.Length - 1) * percentile;
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        double weight = position - lowerIndex;
        return sorted[lowerIndex] * (1.0 - weight) + sorted[upperIndex] * weight;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int mid = sorted.Length / 2;
        if (sorted.Length % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        return sorted[mid];
    }
}
