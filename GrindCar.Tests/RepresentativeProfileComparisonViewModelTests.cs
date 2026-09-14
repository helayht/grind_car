using System.Windows.Media;
using System.Windows;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class RepresentativeProfileComparisonViewModelTests
{
    private const double PlotWidth = 800.0;
    private const double PlotHeight = 500.0;

    [Fact]
    public void Constructor_WithRepresentativePoints_InitializesSummaryText()
    {
        var viewModel = new RepresentativeProfileComparisonViewModel(CreateProfilePoints());

        Assert.Equal("3", viewModel.PointCountText);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.XRangeText));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.YRangeText));
        Assert.Same(Geometry.Empty, viewModel.RepresentativeCurveGeometry);
        Assert.Same(Geometry.Empty, viewModel.StandardCurveGeometry);
        Assert.Empty(viewModel.RepresentativePointItems);
    }

    [Fact]
    public void UpdatePlot_WithValidPlotSize_RefreshesCurvesAxesAndPointItems()
    {
        var viewModel = new RepresentativeProfileComparisonViewModel(CreateProfilePoints());

        viewModel.UpdatePlot(PlotWidth, PlotHeight);

        Assert.False(viewModel.RepresentativeCurveGeometry.IsEmpty());
        Assert.False(viewModel.StandardCurveGeometry.IsEmpty());
        Assert.Equal(CreateProfilePoints().Length, viewModel.RepresentativePointItems.Count);
        Assert.True(viewModel.XAxisX2 > viewModel.XAxisX1);
        Assert.Equal(viewModel.XAxisY1, viewModel.XAxisY2);
        Assert.Equal(viewModel.YAxisX1, viewModel.YAxisX2);
        Assert.True(viewModel.YAxisY2 > viewModel.YAxisY1);
    }

    [Fact]
    public void UpdatePlot_WithSmallPlotSize_ClearsPlotData()
    {
        var viewModel = new RepresentativeProfileComparisonViewModel(CreateProfilePoints());

        viewModel.UpdatePlot(PlotWidth, PlotHeight);
        viewModel.UpdatePlot(10.0, 10.0);

        Assert.True(viewModel.RepresentativeCurveGeometry.IsEmpty());
        Assert.True(viewModel.StandardCurveGeometry.IsEmpty());
        Assert.Empty(viewModel.RepresentativePointItems);
        Assert.Equal(0.0, viewModel.XAxisX1);
        Assert.Equal(0.0, viewModel.XAxisX2);
    }

    [Fact]
    public void Constructor_WithPolylineStyle_UsesMaximumDropLabels()
    {
        var viewModel = new RepresentativeProfileComparisonViewModel(
            CreateProfilePoints(),
            RepresentativeProfileCurveStyle.Polyline);

        Assert.Contains("最大掉块", viewModel.HeaderText);
        Assert.Contains("原始折线", viewModel.CurveDescriptionText);
    }

    [Fact]
    public void Constructor_WithProfileSegments_PreservesAllPointItems()
    {
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments = new IReadOnlyList<RailProfilePoint>[]
        {
            new[]
            {
                new RailProfilePoint(-2.0, 1.0),
                new RailProfilePoint(-1.0, 2.0)
            },
            new[]
            {
                new RailProfilePoint(1.0, 2.0),
                new RailProfilePoint(2.0, 1.0)
            }
        };
        var viewModel = new RepresentativeProfileComparisonViewModel(
            segments,
            RepresentativeProfileCurveStyle.Polyline);

        viewModel.UpdatePlot(PlotWidth, PlotHeight);

        Assert.Equal("4", viewModel.PointCountText);
        Assert.Equal(4, viewModel.RepresentativePointItems.Count);
    }

    [Fact]
    public void Constructor_WithBothMaximumDropProfiles_DrawsBothSidesAndStatuses()
    {
        MaximumDropProfileResult left = CreateMaximumDropProfile(
            PointCloudDeviceSide.Left,
            -4.0,
            0.8);
        MaximumDropProfileResult right = CreateMaximumDropProfile(
            PointCloudDeviceSide.Right,
            2.0,
            0.6);
        var viewModel = new RepresentativeProfileComparisonViewModel(left, right);

        viewModel.UpdatePlot(PlotWidth, PlotHeight);

        Assert.Equal(Visibility.Visible, viewModel.MaximumDropLegendVisibility);
        Assert.Contains("Left 3", viewModel.PointCountText);
        Assert.Contains("Right 3", viewModel.PointCountText);
        Assert.Contains("0.800", viewModel.LeftStatusText);
        Assert.Contains("0.600", viewModel.RightStatusText);
        Assert.False(viewModel.LeftCurveGeometry.IsEmpty());
        Assert.False(viewModel.RightCurveGeometry.IsEmpty());
        Assert.False(viewModel.StandardCurveGeometry.IsEmpty());
        Assert.Equal(3, viewModel.LeftPointItems.Count);
        Assert.Equal(3, viewModel.RightPointItems.Count);
        Assert.Empty(viewModel.RepresentativePointItems);
        Assert.Equal(2, viewModel.MaximumDropMarkerItems.Count);
        Assert.Contains(viewModel.MaximumDropMarkerItems, marker => marker.Label.Contains("Left"));
        Assert.Contains(viewModel.MaximumDropMarkerItems, marker => marker.Label.Contains("Right"));
    }

    [Fact]
    public void Constructor_WithMissingLeftProfile_DrawsRightAndShowsMissingStatus()
    {
        MaximumDropProfileResult right = CreateMaximumDropProfile(
            PointCloudDeviceSide.Right,
            2.0,
            0.6);
        var viewModel = new RepresentativeProfileComparisonViewModel(null, right);

        viewModel.UpdatePlot(PlotWidth, PlotHeight);

        Assert.Contains("未检测到有效掉块", viewModel.LeftStatusText);
        Assert.True(viewModel.LeftCurveGeometry.IsEmpty());
        Assert.Empty(viewModel.LeftPointItems);
        Assert.False(viewModel.RightCurveGeometry.IsEmpty());
        Assert.Equal(3, viewModel.RightPointItems.Count);
        Assert.Single(viewModel.MaximumDropMarkerItems);
        Assert.Contains("Right", viewModel.MaximumDropMarkerItems[0].Label);
    }

    [Fact]
    public void UpdatePlot_MaximumDropWithSmallPlot_ClearsBothSides()
    {
        var viewModel = new RepresentativeProfileComparisonViewModel(
            CreateMaximumDropProfile(PointCloudDeviceSide.Left, -4.0, 0.8),
            CreateMaximumDropProfile(PointCloudDeviceSide.Right, 2.0, 0.6));

        viewModel.UpdatePlot(PlotWidth, PlotHeight);
        viewModel.UpdatePlot(10.0, 10.0);

        Assert.True(viewModel.LeftCurveGeometry.IsEmpty());
        Assert.True(viewModel.RightCurveGeometry.IsEmpty());
        Assert.True(viewModel.StandardCurveGeometry.IsEmpty());
        Assert.Empty(viewModel.LeftPointItems);
        Assert.Empty(viewModel.RightPointItems);
        Assert.Empty(viewModel.MaximumDropMarkerItems);
    }

    [Fact]
    public void NewViewModel_MaximumDropProfiles_DescribesStandardAlignedCurves()
    {
        var viewModel = new RepresentativeProfileComparisonViewModel(
            CreateMaximumDropProfile(PointCloudDeviceSide.Left, -4.0, 0.8),
            CreateMaximumDropProfile(PointCloudDeviceSide.Right, 2.0, 0.6));

        Assert.Contains("标准对齐后", viewModel.HeaderText);
        Assert.Contains("红色标记", viewModel.CurveDescriptionText);
        Assert.Contains("最大掉块 0.800 mm", viewModel.LeftStatusText);
    }

    private static RailProfilePoint[] CreateProfilePoints()
    {
        return new[]
        {
            new RailProfilePoint(-2.0, 1.0),
            new RailProfilePoint(0.0, 2.0),
            new RailProfilePoint(2.0, 1.0)
        };
    }

    private static MaximumDropProfileResult CreateMaximumDropProfile(
        PointCloudDeviceSide side,
        double startX,
        double maximumDropDepth)
    {
        return new MaximumDropProfileResult(
            side,
            1,
            10.0,
            maximumDropDepth,
            new[]
            {
                new RailProfilePoint(startX, -170.0),
                new RailProfilePoint(startX + 1.0, -171.0),
                new RailProfilePoint(startX + 2.0, -170.0)
            });
    }
}
