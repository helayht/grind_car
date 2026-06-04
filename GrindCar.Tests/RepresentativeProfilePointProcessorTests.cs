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

        double lowMinGap = ResolveMinimumStandardGap(lowAlignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);
        double highMinGap = ResolveMinimumStandardGap(highAlignedPoints, point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);

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

        double minGap = ResolveMinimumStandardGap(
            alignedPoints,
            point => point.X > StandardRailProfileSolver.LeftBoundaryX + 1.0);

        Assert.Equal(0.0, minGap, 6);
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
        List<RailProfilePoint> points = RotatePoints(
            BuildLeftHalfProfilePoints(),
            40.0,
            new RailProfilePoint(LeftBoundarySourceX, 4.5));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepresentativeProfilePointProcessor.AlignRepresentativePointsToStandardBoundary(points, PointCloudDeviceSide.Left));

        Assert.Contains("超过可修正范围", exception.Message);
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
        double minGap = ResolveMinimumStandardGap(points, workSurfacePredicate);
        Assert.Equal(0.0, minGap, 6);

        foreach (RailProfilePoint point in points.Where(workSurfacePredicate))
        {
            double standardY = StandardRailProfileSolver.RailSurfaceFun(point.X);
            if (double.IsNaN(standardY) || double.IsInfinity(standardY))
            {
                continue;
            }

            Assert.True(point.Y >= standardY - 1e-6, $"点云点低于标准曲线: X={point.X}, Y={point.Y}, StandardY={standardY}");
        }
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
