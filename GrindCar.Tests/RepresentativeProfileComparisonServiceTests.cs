using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
using Xunit;

namespace GrindCar.Tests;

public class RepresentativeProfileComparisonServiceTests
{
    private const double PlotWidth = 800.0;
    private const double PlotHeight = 500.0;
    private const double PlotPadding = 36.0;

    [Fact]
    public void BuildSnapshot_WithRepresentativePoints_SortsPointsAndBuildsStandardCurve()
    {
        var service = new RepresentativeProfileComparisonService();

        RepresentativeProfileComparisonSnapshot snapshot = service.BuildSnapshot(CreateUnsortedProfilePoints());

        Assert.Equal("5", snapshot.PointCountText);
        Assert.Equal(CreateUnsortedProfilePoints().Count, snapshot.AlignedRepresentativePoints.Count);
        Assert.True(snapshot.AlignedRepresentativePoints.SequenceEqual(
            snapshot.AlignedRepresentativePoints.OrderBy(point => point.X)));
        Assert.NotEmpty(snapshot.StandardPoints);
        Assert.True(snapshot.Bounds.MinX < snapshot.Bounds.MaxX);
        Assert.True(snapshot.Bounds.MinY < snapshot.Bounds.MaxY);
    }

    [Fact]
    public void BuildPlot_WithRepresentativePoints_BuildsStandardAndRepresentativeCurves()
    {
        var service = new RepresentativeProfileComparisonService();
        RepresentativeProfileComparisonSnapshot snapshot = service.BuildSnapshot(CreateProfilePoints());

        RepresentativeProfilePlotResult plot = service.BuildPlot(snapshot, PlotWidth, PlotHeight);

        Assert.False(plot.StandardCurveGeometry.IsEmpty());
        Assert.False(plot.RepresentativeCurveGeometry.IsEmpty());
        Assert.Equal(CreateProfilePoints().Count, plot.RepresentativePoints.Count);
        Assert.All(plot.RepresentativePoints, AssertPointInsidePlot);
        Assert.InRange(plot.XAxisX1, PlotPadding, PlotWidth - PlotPadding);
        Assert.InRange(plot.XAxisX2, PlotPadding, PlotWidth - PlotPadding);
        Assert.InRange(plot.YAxisY1, PlotPadding, PlotHeight - PlotPadding);
        Assert.InRange(plot.YAxisY2, PlotPadding, PlotHeight - PlotPadding);
    }

    [Fact]
    public void BuildPlot_WithRepeatedX_KeepsEveryPointAndBuildsRepresentativeCurve()
    {
        var service = new RepresentativeProfileComparisonService();
        var points = new[]
        {
            new RailProfilePoint(-2.0, -168.0),
            new RailProfilePoint(0.0, -167.0),
            new RailProfilePoint(0.0, -166.5),
            new RailProfilePoint(2.0, -168.0)
        };
        RepresentativeProfileComparisonSnapshot snapshot = service.BuildSnapshot(points);

        RepresentativeProfilePlotResult plot = service.BuildPlot(snapshot, PlotWidth, PlotHeight);

        Assert.Equal(points.Length, plot.RepresentativePoints.Count);
        Assert.False(plot.RepresentativeCurveGeometry.IsEmpty());
    }

    [Fact]
    public void BuildPlot_WithPolylineStyle_UsesFewerSegmentsThanSmoothStyle()
    {
        var service = new RepresentativeProfileComparisonService();
        RepresentativeProfileComparisonSnapshot snapshot = service.BuildSnapshot(CreateProfilePoints());

        RepresentativeProfilePlotResult smoothPlot = service.BuildPlot(
            snapshot,
            PlotWidth,
            PlotHeight,
            RepresentativeProfileCurveStyle.Smooth);
        RepresentativeProfilePlotResult polylinePlot = service.BuildPlot(
            snapshot,
            PlotWidth,
            PlotHeight,
            RepresentativeProfileCurveStyle.Polyline);

        Assert.NotEqual(
            smoothPlot.RepresentativeCurveGeometry.ToString(),
            polylinePlot.RepresentativeCurveGeometry.ToString());
    }

    [Fact]
    public void BuildPlot_WithSegmentedPolyline_CreatesIndependentFigures()
    {
        var service = new RepresentativeProfileComparisonService();
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments = new IReadOnlyList<RailProfilePoint>[]
        {
            new[]
            {
                new RailProfilePoint(-4.0, -170.0),
                new RailProfilePoint(-3.0, -169.0),
                new RailProfilePoint(-2.0, -168.0)
            },
            new[]
            {
                new RailProfilePoint(2.0, -168.0),
                new RailProfilePoint(3.0, -169.0),
                new RailProfilePoint(4.0, -170.0)
            }
        };
        RepresentativeProfileComparisonSnapshot snapshot = service.BuildSnapshot(segments);

        RepresentativeProfilePlotResult plot = service.BuildPlot(
            snapshot,
            PlotWidth,
            PlotHeight,
            RepresentativeProfileCurveStyle.Polyline);

        PathGeometry pathGeometry = PathGeometry.CreateFromGeometry(
            plot.RepresentativeCurveGeometry);
        Assert.Equal(2, snapshot.AlignedRepresentativeSegments.Count);
        Assert.Equal(6, plot.RepresentativePoints.Count);
        Assert.Equal(2, pathGeometry.Figures.Count);
    }

    [Fact]
    public void BuildPlot_WithSmallPlot_ReturnsEmptyResult()
    {
        var service = new RepresentativeProfileComparisonService();
        RepresentativeProfileComparisonSnapshot snapshot = service.BuildSnapshot(CreateProfilePoints());

        RepresentativeProfilePlotResult plot = service.BuildPlot(snapshot, PlotPadding, PlotPadding);

        Assert.True(plot.StandardCurveGeometry.IsEmpty());
        Assert.True(plot.RepresentativeCurveGeometry.IsEmpty());
        Assert.Empty(plot.RepresentativePoints);
    }

    [Fact]
    public void BuildMaximumDropSnapshot_WithBothSides_BuildsSharedBoundsAndSeparateSegments()
    {
        var service = new RepresentativeProfileComparisonService();
        MaximumDropProfileResult left = CreateLeftMaximumDropProfile();
        MaximumDropProfileResult right = CreateRightMaximumDropProfile();

        MaximumDropProfileComparisonSnapshot snapshot = service.BuildMaximumDropSnapshot(left, right);

        Assert.Equal("Left 6 / Right 3", snapshot.PointCountText);
        Assert.Equal(2, snapshot.AlignedLeftSegments.Count);
        Assert.Single(snapshot.AlignedRightSegments);
        Assert.Equal(6, snapshot.AlignedLeftPoints.Count);
        Assert.Equal(3, snapshot.AlignedRightPoints.Count);
        Assert.True(snapshot.Bounds.MinX < left.ProfilePoints.Min(point => point.X));
        Assert.True(snapshot.Bounds.MaxX > right.ProfilePoints.Max(point => point.X));
        Assert.NotEmpty(snapshot.StandardPoints);
        Assert.NotNull(snapshot.LeftMaximumDropMarker);
        Assert.NotNull(snapshot.RightMaximumDropMarker);
        Assert.Equal(left.MaximumDropPoint, snapshot.LeftMaximumDropMarker!.OriginalPoint);
        Assert.Equal(left.ShiftedMaximumDropPoint, snapshot.LeftMaximumDropMarker.DisplayPoint);
        Assert.Contains("Left", snapshot.LeftMaximumDropMarker.Label);
        Assert.Contains("原始Z=", snapshot.LeftMaximumDropMarker.Label);
        Assert.Contains("深度=", snapshot.LeftMaximumDropMarker.Label);
    }

    [Fact]
    public void BuildMaximumDropPlot_WithBothSides_PreservesEachSidesSegmentBoundaries()
    {
        var service = new RepresentativeProfileComparisonService();
        MaximumDropProfileComparisonSnapshot snapshot = service.BuildMaximumDropSnapshot(
            CreateLeftMaximumDropProfile(),
            CreateRightMaximumDropProfile());

        MaximumDropProfilePlotResult plot = service.BuildMaximumDropPlot(
            snapshot,
            PlotWidth,
            PlotHeight);

        PathGeometry leftGeometry = PathGeometry.CreateFromGeometry(plot.LeftCurveGeometry);
        PathGeometry rightGeometry = PathGeometry.CreateFromGeometry(plot.RightCurveGeometry);
        Assert.Equal(2, leftGeometry.Figures.Count);
        Assert.Single(rightGeometry.Figures);
        Assert.Equal(6, plot.LeftPoints.Count);
        Assert.Equal(3, plot.RightPoints.Count);
        Assert.False(plot.StandardCurveGeometry.IsEmpty());
        Assert.NotNull(plot.LeftMaximumDropMarker);
        Assert.NotNull(plot.RightMaximumDropMarker);
        Assert.InRange(plot.LeftMaximumDropMarker!.Left, PlotPadding - 6.0, PlotWidth - PlotPadding - 6.0);
        Assert.InRange(plot.LeftMaximumDropMarker.Top, PlotPadding - 6.0, PlotHeight - PlotPadding - 6.0);
        Assert.InRange(plot.RightMaximumDropMarker!.Left, PlotPadding - 6.0, PlotWidth - PlotPadding - 6.0);
        Assert.InRange(plot.RightMaximumDropMarker.Top, PlotPadding - 6.0, PlotHeight - PlotPadding - 6.0);
    }

    [Fact]
    public void BuildMaximumDropPlot_WithOneMissingSide_DrawsAvailableSideOnly()
    {
        var service = new RepresentativeProfileComparisonService();
        MaximumDropProfileComparisonSnapshot snapshot = service.BuildMaximumDropSnapshot(
            null,
            CreateRightMaximumDropProfile());

        MaximumDropProfilePlotResult plot = service.BuildMaximumDropPlot(
            snapshot,
            PlotWidth,
            PlotHeight);

        Assert.True(plot.LeftCurveGeometry.IsEmpty());
        Assert.Empty(plot.LeftPoints);
        Assert.False(plot.RightCurveGeometry.IsEmpty());
        Assert.Equal(3, plot.RightPoints.Count);
        Assert.False(plot.StandardCurveGeometry.IsEmpty());
        Assert.Null(plot.LeftMaximumDropMarker);
        Assert.NotNull(plot.RightMaximumDropMarker);
    }

    [Fact]
    public void BuildMaximumDropPlot_WithSmallPlot_ReturnsAllCurvesEmpty()
    {
        var service = new RepresentativeProfileComparisonService();
        MaximumDropProfileComparisonSnapshot snapshot = service.BuildMaximumDropSnapshot(
            CreateLeftMaximumDropProfile(),
            CreateRightMaximumDropProfile());

        MaximumDropProfilePlotResult plot = service.BuildMaximumDropPlot(
            snapshot,
            PlotPadding,
            PlotPadding);

        Assert.True(plot.LeftCurveGeometry.IsEmpty());
        Assert.True(plot.RightCurveGeometry.IsEmpty());
        Assert.True(plot.StandardCurveGeometry.IsEmpty());
        Assert.Empty(plot.LeftPoints);
        Assert.Empty(plot.RightPoints);
        Assert.Null(plot.LeftMaximumDropMarker);
        Assert.Null(plot.RightMaximumDropMarker);
    }

    [Fact]
    public void BuildMaximumDropSnapshot_UsesProfilesShiftedByStandardAlignmentOffset()
    {
        var service = new RepresentativeProfileComparisonService();
        MaximumDropProfileResult left = CreateLeftMaximumDropProfile();

        MaximumDropProfileComparisonSnapshot snapshot = service.BuildMaximumDropSnapshot(left, null);

        Assert.Equal(left.ShiftedProfilePoints, snapshot.AlignedLeftPoints);
        Assert.NotEqual(left.ProfilePoints[0].Y, snapshot.AlignedLeftPoints[0].Y);
        Assert.Equal(left.ProfilePoints[0].Y + left.StandardAlignmentOffset, snapshot.AlignedLeftPoints[0].Y, 6);
    }

    [Fact]
    public void BuildSnapshot_WithInsufficientPoints_ThrowsInvalidOperationException()
    {
        var service = new RepresentativeProfileComparisonService();

        Assert.Throws<InvalidOperationException>(() =>
            service.BuildSnapshot(new[] { new RailProfilePoint(0.0, -167.0) }));
    }

    private static IReadOnlyList<RailProfilePoint> CreateUnsortedProfilePoints()
    {
        return new[]
        {
            new RailProfilePoint(4.0, -170.0),
            new RailProfilePoint(-4.0, -170.0),
            new RailProfilePoint(0.0, -167.0),
            new RailProfilePoint(2.0, -168.0),
            new RailProfilePoint(-2.0, -168.0)
        };
    }

    private static IReadOnlyList<RailProfilePoint> CreateProfilePoints()
    {
        return new[]
        {
            new RailProfilePoint(-4.0, -170.0),
            new RailProfilePoint(-2.0, -168.0),
            new RailProfilePoint(0.0, -167.0),
            new RailProfilePoint(2.0, -168.0),
            new RailProfilePoint(4.0, -170.0)
        };
    }

    private static MaximumDropProfileResult CreateLeftMaximumDropProfile()
    {
        return new MaximumDropProfileResult(
            PointCloudDeviceSide.Left,
            1,
            10.0,
            0.8,
            new IReadOnlyList<RailProfilePoint>[]
            {
                new[]
                {
                    new RailProfilePoint(-6.0, -170.0),
                    new RailProfilePoint(-5.0, -171.0),
                    new RailProfilePoint(-4.0, -170.0)
                },
                new[]
                {
                    new RailProfilePoint(-3.0, -169.0),
                    new RailProfilePoint(-2.0, -170.0),
                    new RailProfilePoint(-1.0, -169.0)
                }
            });
    }

    private static MaximumDropProfileResult CreateRightMaximumDropProfile()
    {
        return new MaximumDropProfileResult(
            PointCloudDeviceSide.Right,
            2,
            20.0,
            0.6,
            new[]
            {
                new RailProfilePoint(1.0, -169.0),
                new RailProfilePoint(2.0, -170.5),
                new RailProfilePoint(6.0, -170.0)
            });
    }

    private static void AssertPointInsidePlot(RepresentativeProfileScreenPoint point)
    {
        Assert.InRange(point.Left, PlotPadding - 2.5, PlotWidth - PlotPadding - 2.5);
        Assert.InRange(point.Top, PlotPadding - 2.5, PlotHeight - PlotPadding - 2.5);
    }

}
