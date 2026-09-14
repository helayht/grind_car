using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class MaximumDropProfileServiceTests
{
    [Fact]
    public void AnalyzePoints_StandardProfiles_ReturnsNoDropProfile()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, _ => 0.0),
            (2.0, _ => 0.0));

        MaximumDropProfileResult? result = service.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left,
            sampleIndex: 4);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzePoints_ThreePointDrop_SelectsProfileAndKeepsOriginalHeight()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, _ => 0.0),
            (2.0, x => x >= -1.0 && x <= 1.0 ? -2.0 : 0.0));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Right, sampleIndex: 7));

        Assert.Equal(2.0, result.ProfileY, 6);
        Assert.Equal(2.0, result.MaximumDropDepth, 6);
        double maximumPointResidual = result.MaximumDropPoint.Y -
            StandardRailProfileSolver.RailSurfaceFun(result.MaximumDropPoint.X);
        Assert.Equal(-result.MaximumDropDepth, maximumPointResidual, 6);
        Assert.Equal(
            result.MaximumDropPoint.Y + result.StandardAlignmentOffset,
            result.ShiftedMaximumDropPoint.Y,
            6);
        Assert.Equal(result.MaximumDropPoint.X, result.ShiftedMaximumDropPoint.X, 6);
        Assert.Contains(result.ProfilePoints, point =>
            point.Y < StandardRailProfileSolver.RailSurfaceFun(point.X));
    }

    [Fact]
    public void CalculateDefectGrindDepths_ProfileBelowStandard_ReturnsZero()
    {
        var profile = new List<RailProfilePoint>();
        for (int x = -4; x <= 4; x++)
        {
            profile.Add(new RailProfilePoint(
                x,
                StandardRailProfileSolver.RailSurfaceFun(x) - 1.0));
        }

        IReadOnlyList<GrindDepthResult> results =
            RailSurfaceService.CalculateDefectGrindDepths(new[] { 0 }, profile);

        Assert.Equal(0.0, results.Single().GrindDepth, 6);
    }

    [Fact]
    public void CalculateDefectGrindDepths_MaximumDropProfile_UsesStandardAlignmentWithoutChangingOriginalPoints()
    {
        var profile = new MaximumDropProfileResult(
            PointCloudDeviceSide.Left,
            sampleIndex: 1,
            profileY: 2.0,
            maximumDropDepth: 1.0,
            new[]
            {
                new RailProfilePoint(-1.0, StandardRailProfileSolver.RailSurfaceFun(-1.0) - 1.0),
                new RailProfilePoint(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0) - 1.0),
                new RailProfilePoint(1.0, StandardRailProfileSolver.RailSurfaceFun(1.0) - 1.0)
            });

        IReadOnlyList<GrindDepthResult> results =
            RailSurfaceService.CalculateDefectGrindDepths(new[] { 0 }, profile);

        Assert.Equal(0.0, results.Single().GrindDepth, 6);
        Assert.Equal(1.0, profile.StandardAlignmentOffset, 6);
        Assert.Equal(profile.ProfilePoints.Count, profile.ShiftedProfilePoints.Count);
        for (int index = 0; index < profile.ProfilePoints.Count; index++)
        {
            Assert.Equal(profile.ProfilePoints[index].X, profile.ShiftedProfilePoints[index].X, 6);
            Assert.Equal(
                profile.ProfilePoints[index].Y + profile.StandardAlignmentOffset,
                profile.ShiftedProfilePoints[index].Y,
                6);
        }

        Assert.All(profile.ProfilePoints, point =>
            Assert.True(point.Y < StandardRailProfileSolver.RailSurfaceFun(point.X)));
    }

    [Fact]
    public void MaximumDropProfile_StandardAlignmentOffset_UsesAllEffectivePointsInsteadOfMaximumDropDepth()
    {
        var profile = new MaximumDropProfileResult(
            PointCloudDeviceSide.Left,
            sampleIndex: 1,
            profileY: 2.0,
            maximumDropDepth: 0.2,
            new[]
            {
                new RailProfilePoint(-1.0, StandardRailProfileSolver.RailSurfaceFun(-1.0) + 0.1),
                new RailProfilePoint(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0) - 1.5),
                new RailProfilePoint(1.0, StandardRailProfileSolver.RailSurfaceFun(1.0) - 0.3)
            });

        Assert.Equal(0.2, profile.MaximumDropDepth, 6);
        Assert.Equal(1.5, profile.StandardAlignmentOffset, 6);
        Assert.NotEqual(profile.MaximumDropDepth, profile.StandardAlignmentOffset);
        for (int index = 0; index < profile.ShiftedProfilePoints.Count; index++)
        {
            RailProfilePoint point = profile.ShiftedProfilePoints[index];
            Assert.True(point.Y >= StandardRailProfileSolver.RailSurfaceFun(point.X) - 1e-9);
        }
    }

    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    public void CalculateDefectGrindDepths_MaximumDropProfile_VerticalAngleIsUnchangedByStandardAlignment(int angle)
    {
        var profile = new MaximumDropProfileResult(
            PointCloudDeviceSide.Left,
            sampleIndex: 1,
            profileY: 2.0,
            maximumDropDepth: 1.0,
            new[]
            {
                new RailProfilePoint(-35.0, StandardRailProfileSolver.RailSurfaceFun(-35.0) - 1.0),
                new RailProfilePoint(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0) - 1.0),
                new RailProfilePoint(35.0, StandardRailProfileSolver.RailSurfaceFun(35.0) - 1.0)
            });

        double shiftedDepth = RailSurfaceService.CalculateDefectGrindDepths(new[] { angle }, profile).Single().GrindDepth;
        double originalDepth = RailSurfaceService.CalculateDefectGrindDepths(
            new[] { angle },
            profile.ProfilePoints).Single().GrindDepth;

        Assert.True(double.IsFinite(shiftedDepth));
        Assert.Equal(originalDepth, shiftedDepth, 9);
    }

    [Fact]
    public void AnalyzePoints_SingleLowOutlier_DoesNotFormValidDrop()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => Math.Abs(x) < 0.1 ? -5.0 : 0.0));

        MaximumDropProfileResult? result = service.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzePoints_FirstHeightFilterRetainsOriginalEndpointsAndResiduals()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloudRange(
            -35,
            35,
            (1.0, x => x switch
            {
                -1.0 => -1.0,
                0.0 => -2.0,
                1.0 => -1.0,
                _ => 0.0
            }));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Contains(result.ProfilePoints, point => point.X == -35.0);
        Assert.Contains(result.ProfilePoints, point => point.X == 35.0);
        RailProfilePoint deepestPoint = Assert.Single(
            result.ProfilePoints.Where(point => point.X == 0.0));
        Assert.Equal(StandardRailProfileSolver.RailSurfaceFun(0.0) - 2.0, deepestPoint.Y, 6);
        Assert.Equal(2.0, result.MaximumDropDepth, 6);
        Assert.Equal(deepestPoint, result.MaximumDropPoint);
    }

    [Fact]
    public void AnalyzePoints_IsolatedLowPointAlongsideValidDrop_RemainsButDoesNotSetMaximumDrop()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x =>
            {
                if (x == -10.0)
                {
                    return -2.5;
                }

                return x >= 0.0 && x <= 2.0 ? -1.0 : 0.0;
            }));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Right));

        RailProfilePoint isolatedPoint = Assert.Single(
            result.ProfilePoints.Where(point => point.X == -10.0));
        Assert.Equal(StandardRailProfileSolver.RailSurfaceFun(-10.0) - 2.5, isolatedPoint.Y, 6);
        Assert.Equal(1.0, result.MaximumDropDepth, 6);
        Assert.InRange(result.MaximumDropPoint.X, 0.0, 2.0);
        Assert.Equal(2.5, result.StandardAlignmentOffset, 6);
    }

    [Fact]
    public void AnalyzePoints_PointsBelowStandardMinimumHeight_AreRemoved()
    {
        MaximumDropProfileService service = CreateService();
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloudRange(
            -8,
            8,
            (1.0, x => x >= -1.0 && x <= 1.0
                ? minimumHeight - StandardRailProfileSolver.RailSurfaceFun(x) - 1.0
                : 0.0));

        MaximumDropProfileResult? result = service.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzePoints_PointAboveGlobalMinimumButBelowRangeMinimum_IsRemoved()
    {
        MaximumDropProfileService service = CreateService();
        double globalMinimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        double rangeMinimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight(-8.0, 8.0);
        double pointHeightBetweenThresholds =
            (globalMinimumHeight + rangeMinimumHeight) / 2.0;
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloudRange(
            -8,
            8,
            (1.0, x =>
            {
                if (x == 0.0)
                {
                    return pointHeightBetweenThresholds -
                           StandardRailProfileSolver.RailSurfaceFun(x);
                }

                return x >= 3.0 && x <= 5.0 ? -0.01 : 0.0;
            }));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Right));

        Assert.Equal(0.01, result.MaximumDropDepth, 6);
        Assert.DoesNotContain(result.ProfilePoints, point => point.X == 0.0);
        Assert.All(result.ProfilePoints, point => Assert.True(point.Y >= rangeMinimumHeight));
    }

    [Fact]
    public void AnalyzePoints_DifferentSectionXRanges_UseIndependentMinimumHeights()
    {
        MaximumDropProfileService service = CreateService();
        var points = new List<PointCloudPoint3D>();
        AddSectionPoints(points, -8, 8, 1.0, x => x >= -1.0 && x <= 1.0 ? -1.0 : 0.0);
        AddSectionPoints(points, -35, 35, 2.0, x => x >= -1.0 && x <= 1.0 ? -1.0 : 0.0);

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Equal(2.0, result.ProfileY, 6);
        Assert.Equal(1.0, result.MaximumDropDepth, 6);
    }

    [Theory]
    [InlineData(PointCloudDeviceSide.Left)]
    [InlineData(PointCloudDeviceSide.Right)]
    public void AnalyzePoints_SecondHeightFilterUsesFirstPassEffectiveRange(
        PointCloudDeviceSide side)
    {
        MaximumDropProfileService service = CreateService();
        double firstMinimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight(0.0, 35.4);
        double secondMinimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight(0.0, 35.123);
        const double lowPointX = 35.105;
        const double lowPointZ = -13.692;
        var points = new[]
        {
            new PointCloudPoint3D(0.0, 1.0, StandardRailProfileSolver.RailSurfaceFun(0.0) - 1.0),
            new PointCloudPoint3D(1.0, 1.0, StandardRailProfileSolver.RailSurfaceFun(1.0) - 1.0),
            new PointCloudPoint3D(2.0, 1.0, StandardRailProfileSolver.RailSurfaceFun(2.0) - 1.0),
            new PointCloudPoint3D(lowPointX, 1.0, lowPointZ),
            new PointCloudPoint3D(35.121, 1.0, StandardRailProfileSolver.RailSurfaceFun(35.121)),
            new PointCloudPoint3D(35.122, 1.0, StandardRailProfileSolver.RailSurfaceFun(35.122)),
            new PointCloudPoint3D(35.123, 1.0, StandardRailProfileSolver.RailSurfaceFun(35.123)),
            new PointCloudPoint3D(35.4, 1.0, firstMinimumHeight - 0.1)
        };

        Assert.True(lowPointZ >= firstMinimumHeight);
        Assert.True(lowPointZ < secondMinimumHeight);

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, side));

        Assert.DoesNotContain(result.ProfilePoints, point => point.X == lowPointX);
        Assert.Equal(35.123, result.ProfilePoints.Max(point => point.X), 6);
        Assert.All(result.ProfilePoints, point => Assert.True(point.Y >= secondMinimumHeight));
        Assert.Equal(2, result.ProfileSegments.Count);
        Assert.DoesNotContain(result.ProfileSegments, segment =>
            segment.Min(point => point.X) < lowPointX && segment.Max(point => point.X) > lowPointX);
        Assert.Equal(1.0, result.MaximumDropDepth, 6);
    }

    [Fact]
    public void AnalyzePoints_SecondHeightFilterRemovesNewShortSegments()
    {
        MaximumDropProfileService service = CreateService();
        double firstMinimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight(0.0, 35.4);
        var points = new[]
        {
            new PointCloudPoint3D(0.0, 1.0, StandardRailProfileSolver.RailSurfaceFun(0.0) - 1.0),
            new PointCloudPoint3D(1.0, 1.0, StandardRailProfileSolver.RailSurfaceFun(1.0) - 1.0),
            new PointCloudPoint3D(2.0, 1.0, StandardRailProfileSolver.RailSurfaceFun(2.0) - 1.0),
            new PointCloudPoint3D(35.105, 1.0, -13.692),
            new PointCloudPoint3D(35.122, 1.0, StandardRailProfileSolver.RailSurfaceFun(35.122)),
            new PointCloudPoint3D(35.123, 1.0, StandardRailProfileSolver.RailSurfaceFun(35.123)),
            new PointCloudPoint3D(35.4, 1.0, firstMinimumHeight - 0.1)
        };

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Right));

        IReadOnlyList<RailProfilePoint> segment = Assert.Single(result.ProfileSegments);
        Assert.Equal(new[] { 0.0, 1.0, 2.0 }, segment.Select(point => point.X));
        Assert.Equal(segment, result.ProfilePoints);
    }

    [Fact]
    public void AnalyzePoints_FirstHeightFilterPreventsDropRunsFromCrossingGap()
    {
        MaximumDropProfileService service = CreateService();
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloudRange(
            -35,
            35,
            (1.0, x =>
            {
                if (x == 0.0)
                {
                    return minimumHeight - StandardRailProfileSolver.RailSurfaceFun(x) - 1.0;
                }

                return (x >= -2.0 && x <= -1.0) || (x >= 1.0 && x <= 2.0)
                    ? -1.0
                    : 0.0;
            }));

        MaximumDropProfileResult? result = service.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzePoints_HeightFilterGap_ReturnsIndependentProfileSegments()
    {
        MaximumDropProfileService service = CreateService();
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloudRange(
            -35,
            35,
            (1.0, x =>
            {
                if (x == 0.0)
                {
                    return minimumHeight - StandardRailProfileSolver.RailSurfaceFun(x) - 1.0;
                }

                return x >= 2.0 && x <= 6.0 ? -1.0 : 0.0;
            }));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Right));

        Assert.Equal(1.0, result.MaximumDropDepth, 6);
        Assert.Equal(2, result.ProfileSegments.Count);
        Assert.All(result.ProfileSegments, segment => Assert.True(segment.Count >= 3));
        Assert.Equal(
            result.ProfileSegments.SelectMany(segment => segment),
            result.ProfilePoints);
        Assert.All(result.ProfilePoints, point => Assert.True(point.Y >= minimumHeight));
    }

    [Fact]
    public void FilterByMinimumProfileHeight_ReconstructedPointsRemovesBelowAndKeepsEqual()
    {
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        var reconstructedPoints = new[]
        {
            new RailProfilePoint(-1.0, minimumHeight - 0.1),
            new RailProfilePoint(0.0, minimumHeight),
            new RailProfilePoint(1.0, minimumHeight + 0.1)
        };

        List<RailProfilePoint> result = MaximumDropProfileService.FilterByMinimumProfileHeight(
            reconstructedPoints,
            minimumHeight);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, point => point.Y < minimumHeight);
        Assert.Contains(result, point => point.Y == minimumHeight);
    }

    [Fact]
    public void SplitByMinimumProfileHeight_BelowPointCreatesHardSegmentBoundary()
    {
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        var points = new[]
        {
            new RailProfilePoint(-2.0, minimumHeight + 0.1),
            new RailProfilePoint(-1.0, minimumHeight - 0.1),
            new RailProfilePoint(0.0, minimumHeight),
            new RailProfilePoint(1.0, minimumHeight + 0.1)
        };

        List<List<RailProfilePoint>> segments =
            MaximumDropProfileService.SplitByMinimumProfileHeight(points, minimumHeight);

        Assert.Equal(2, segments.Count);
        Assert.Single(segments[0]);
        Assert.Equal(2, segments[1].Count);
        Assert.Equal(minimumHeight, segments[1][0].Y, 6);
    }

    [Fact]
    public void MaximumDropProfileResult_FiveArgumentConstructorCreatesSingleSegment()
    {
        var points = new[]
        {
            new RailProfilePoint(-1.0, 1.0),
            new RailProfilePoint(0.0, 2.0),
            new RailProfilePoint(1.0, 1.0)
        };

        var result = new MaximumDropProfileResult(
            PointCloudDeviceSide.Left,
            1,
            2.0,
            0.5,
            points);

        IReadOnlyList<RailProfilePoint> segment = Assert.Single(result.ProfileSegments);
        Assert.Equal(points, segment);
        Assert.Equal(points, result.ProfilePoints);
    }

    [Fact]
    public void TryCalculateMaximumDownwardDepth_ReconstructedPointBelowMinimumBreaksContinuity()
    {
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        var reconstructedPoints = new[]
        {
            BuildProfilePoint(-3.0, -1.0),
            BuildProfilePoint(-2.0, -1.0),
            new RailProfilePoint(-1.0, minimumHeight - 0.1),
            BuildProfilePoint(0.0, -1.0),
            BuildProfilePoint(1.0, -1.0)
        };

        bool result = MaximumDropProfileService.TryCalculateMaximumDownwardDepth(
            reconstructedPoints,
            minimumHeight,
            out double maximumDropDepth);

        Assert.False(result);
        Assert.Equal(0.0, maximumDropDepth, 6);
    }

    [Fact]
    public void TryCalculateMaximumDownwardDepth_ThreeValidPointsReturnsDeepestDrop()
    {
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        var reconstructedPoints = new[]
        {
            BuildProfilePoint(-1.0, -1.0),
            BuildProfilePoint(0.0, -2.0),
            BuildProfilePoint(1.0, -1.5)
        };

        bool result = MaximumDropProfileService.TryCalculateMaximumDownwardDepth(
            reconstructedPoints,
            minimumHeight,
            out double maximumDropDepth);

        Assert.True(result);
        Assert.Equal(2.0, maximumDropDepth, 6);
    }

    [Fact]
    public void FilterByMinimumProfileHeight_FewerThanThreeReconstructedPointsRemain()
    {
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        var reconstructedPoints = new[]
        {
            new RailProfilePoint(-1.0, minimumHeight - 0.1),
            new RailProfilePoint(0.0, minimumHeight),
            new RailProfilePoint(1.0, minimumHeight + 0.1)
        };

        List<RailProfilePoint> result = MaximumDropProfileService.FilterByMinimumProfileHeight(
            reconstructedPoints,
            minimumHeight);

        Assert.True(result.Count < 3);
    }

    [Fact]
    public void AnalyzePoints_SectionWithTooFewPointsAfterHeightFilter_IsSkipped()
    {
        MaximumDropProfileService service = CreateService();
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => x <= -33.0
                ? 0.0
                : minimumHeight - StandardRailProfileSolver.RailSurfaceFun(x) - 1.0),
            (2.0, x => x >= -1.0 && x <= 1.0 ? -1.5 : 0.0));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Equal(2.0, result.ProfileY, 6);
        Assert.Equal(1.5, result.MaximumDropDepth, 6);
    }

    [Fact]
    public void AnalyzePoints_AllPointsBelowStandardMinimumHeight_Throws()
    {
        MaximumDropProfileService service = CreateService();
        double minimumHeight = StandardRailProfileSolver.GetMinimumProfileHeight();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => minimumHeight - StandardRailProfileSolver.RailSurfaceFun(x) - 1.0));

        RepresentativeProfileExtractionException exception = Assert.Throws<RepresentativeProfileExtractionException>(
            () => service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Contains("连续有效点", exception.Message);
    }

    [Fact]
    public void AnalyzePoints_ProfileEntirelyAboveStandard_ReturnsNoDropProfile()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => x >= -1.0 && x <= 1.0 ? 1.0 : 2.0));

        MaximumDropProfileResult? result = service.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzePoints_TwoConsecutiveNegativeResiduals_ReturnsNoDropProfile()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => x >= 0.0 && x <= 1.0 ? -2.0 : 0.0));

        MaximumDropProfileResult? result = service.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzePoints_PositiveResidualsDoNotIncreaseDownwardDropDepth()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => x >= -1.0 && x <= 1.0 ? -2.0 : 10.0));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Equal(2.0, result.MaximumDropDepth, 6);
    }

    [Fact]
    public void AnalyzePoints_DropDepthAboveMaximumAllowed_IsExcluded()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => x >= -1.0 && x <= 1.0 ? -4.0 : 0.0),
            (2.0, x => x >= -1.0 && x <= 1.0 ? -2.0 : 0.0));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Equal(2.0, result.ProfileY, 6);
        Assert.Equal(2.0, result.MaximumDropDepth, 6);
    }

    [Fact]
    public void AnalyzePoints_DropDepthEqualToMaximumAllowed_IsRetained()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, _ => 0.0),
            (2.0, x => x >= -1.0 && x <= 1.0
                ? -MaximumDropProfileService.MaximumAllowedDropDepth
                : 0.0));

        MaximumDropProfileResult result = Assert.IsType<MaximumDropProfileResult>(
            service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Equal(2.0, result.ProfileY, 6);
        Assert.Equal(MaximumDropProfileService.MaximumAllowedDropDepth, result.MaximumDropDepth, 6);
    }

    [Fact]
    public void AnalyzePoints_AllDropDepthsAboveMaximumAllowed_Throws()
    {
        MaximumDropProfileService service = CreateService();
        IReadOnlyList<PointCloudPoint3D> points = BuildPointCloud(
            (1.0, x => x >= -1.0 && x <= 1.0 ? -4.0 : 0.0),
            (2.0, x => x >= -1.0 && x <= 1.0 ? -5.0 : 0.0));

        RepresentativeProfileExtractionException exception = Assert.Throws<RepresentativeProfileExtractionException>(
            () => service.AnalyzePoints(points, PointCloudDeviceSide.Left));

        Assert.Contains("均大于 3", exception.Message);
    }

    [Fact]
    public void CalculateDefectGrindDepths_ProfileAboveStandard_UsesProfileMinusStandard()
    {
        var profile = new[]
        {
            new RailProfilePoint(-1.0, StandardRailProfileSolver.RailSurfaceFun(-1.0) + 1.0),
            new RailProfilePoint(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0) + 1.0),
            new RailProfilePoint(1.0, StandardRailProfileSolver.RailSurfaceFun(1.0) + 1.0)
        };

        IReadOnlyList<GrindDepthResult> results =
            RailSurfaceService.CalculateDefectGrindDepths(new[] { 0 }, profile);

        Assert.Equal(1.0, results.Single().GrindDepth, 6);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    public void CalculateDefectGrindDepths_VerticalAngle_ReturnsFiniteHorizontalGap(int angle)
    {
        var profile = new[]
        {
            new RailProfilePoint(-35.0, StandardRailProfileSolver.RailSurfaceFun(-35.0)),
            new RailProfilePoint(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0)),
            new RailProfilePoint(35.0, StandardRailProfileSolver.RailSurfaceFun(35.0))
        };

        GrindDepthResult result = RailSurfaceService.CalculateDefectGrindDepths(
            new[] { angle },
            profile).Single();

        Assert.True(double.IsFinite(result.GrindDepth));
        Assert.Equal(0.0, result.GrindDepth, 9);
    }

    [Theory]
    [InlineData(PointCloudDeviceSide.Left, -20, false)]
    [InlineData(PointCloudDeviceSide.Left, 20, true)]
    [InlineData(PointCloudDeviceSide.Right, -20, true)]
    [InlineData(PointCloudDeviceSide.Right, 20, false)]
    [InlineData(PointCloudDeviceSide.Left, 0, true)]
    [InlineData(PointCloudDeviceSide.Right, 0, true)]
    public void IsDefectAngleApplicable_UsesMatchingRailSide(
        PointCloudDeviceSide side,
        int angle,
        bool expected)
    {
        bool result = RailSurfaceService.IsDefectAngleApplicable(side, angle);

        Assert.Equal(expected, result);
    }

    private static MaximumDropProfileService CreateService()
    {
        string settingsPath = Path.Combine(
            Path.GetTempPath(),
            "grindcar-tests",
            Guid.NewGuid().ToString("N"),
            "point-cloud-profile-registration.json");
        var store = new ProfileRegistrationSettingsStore(settingsPath);
        store.Save(new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(),
            Right = new ProfileRegistrationParameters()
        });
        return new MaximumDropProfileService(store, new ProfileRegistrationTransformService());
    }

    private static RailProfilePoint BuildProfilePoint(double x, double residual)
    {
        return new RailProfilePoint(
            x,
            StandardRailProfileSolver.RailSurfaceFun(x) + residual);
    }

    private static IReadOnlyList<PointCloudPoint3D> BuildPointCloud(
        params (double Y, Func<double, double> ResidualSelector)[] sections)
    {
        return BuildPointCloudRange(-35, 35, sections);
    }

    private static IReadOnlyList<PointCloudPoint3D> BuildPointCloudRange(
        int minimumX,
        int maximumX,
        params (double Y, Func<double, double> ResidualSelector)[] sections)
    {
        var points = new List<PointCloudPoint3D>();
        for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            (double y, Func<double, double> residualSelector) = sections[sectionIndex];
            AddSectionPoints(points, minimumX, maximumX, y, residualSelector);
        }

        return points;
    }

    private static void AddSectionPoints(
        ICollection<PointCloudPoint3D> points,
        int minimumX,
        int maximumX,
        double y,
        Func<double, double> residualSelector)
    {
        for (int x = minimumX; x <= maximumX; x++)
        {
            double standardZ = StandardRailProfileSolver.RailSurfaceFun(x);
            points.Add(new PointCloudPoint3D(x, y, standardZ + residualSelector(x)));
        }
    }
}
