using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Debug;
using GrindCar.Services.Rail.Processing;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudGrindDepthDebugViewModelTests
{
    [Fact]
    public void NewViewModel_WithoutFiles_CannotCalculate()
    {
        var viewModel = new PointCloudGrindDepthDebugViewModel(
            new PointCloudGrindDepthDebugWorkflowService(
                new FakeProfileService(),
                RailSurfaceService.CalculateGrindDepths));

        Assert.False(viewModel.CanCalculate);
    }

    [Fact]
    public async Task CalculateAsync_WithValidFiles_PopulatesResultsAndRepresentativePoints()
    {
        var viewModel = new PointCloudGrindDepthDebugViewModel(
            new PointCloudGrindDepthDebugWorkflowService(
                new FakeProfileService(),
                (angles, points) => new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, angle * 0.01)).ToArray(),
                    points)));
        viewModel.SetLeftFilePath("left.csv");
        viewModel.SetRightFilePath("right.csv");
        viewModel.AnglesInput = "0,10";

        IReadOnlyList<RailProfilePoint> representativePoints = await viewModel.CalculateAsync();

        Assert.Equal(2, viewModel.Results.Count);
        Assert.Equal(4, representativePoints.Count);
        Assert.Equal(4, viewModel.LatestRepresentativePoints.Count);
        Assert.Contains("计算完成", viewModel.StatusMessage);
        Assert.False(viewModel.CanViewMaximumDropProfile);
    }

    [Fact]
    public async Task CalculateAsync_WithSegmentedMaximumDrop_PreservesProfileSegments()
    {
        var viewModel = new PointCloudGrindDepthDebugViewModel(
            new PointCloudGrindDepthDebugWorkflowService(
                new FakeSegmentedProfileAnalysisService(),
                (angles, points) => new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.2)).ToArray(),
                    points),
                (angles, points) => angles
                    .Select(angle => new GrindDepthResult(angle, 0.5))
                    .ToArray()));
        viewModel.SetLeftFilePath("left.csv");
        viewModel.SetRightFilePath("right.csv");
        viewModel.AnglesInput = "0";

        await viewModel.CalculateAsync();

        Assert.True(viewModel.CanViewMaximumDropProfile);
        Assert.NotNull(viewModel.LatestLeftMaximumDropProfile);
        Assert.Null(viewModel.LatestRightMaximumDropProfile);
        Assert.Equal(2, viewModel.LatestLeftMaximumDropProfileSegments.Count);
        Assert.Empty(viewModel.LatestRightMaximumDropProfileSegments);
        Assert.Equal(2, viewModel.LatestMaximumDropProfileSegments.Count);
        Assert.Equal(6, viewModel.LatestMaximumDropProfilePoints.Count);
        Assert.Equal(
            viewModel.LatestMaximumDropProfileSegments.SelectMany(segment => segment),
            viewModel.LatestMaximumDropProfilePoints);
    }

    [Fact]
    public async Task CalculateAsync_WithBothMaximumDropProfiles_CachesBothAndKeepsGlobalDeeperProfile()
    {
        var viewModel = new PointCloudGrindDepthDebugViewModel(
            new PointCloudGrindDepthDebugWorkflowService(
                new FakeDualMaximumDropProfileAnalysisService(),
                (angles, points) => new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.2)).ToArray(),
                    points),
                (angles, points) => angles
                    .Select(angle => new GrindDepthResult(angle, 0.5))
                    .ToArray()));
        viewModel.SetLeftFilePath("left.csv");
        viewModel.SetRightFilePath("right.csv");
        viewModel.AnglesInput = "0";

        await viewModel.CalculateAsync();

        Assert.True(viewModel.CanViewMaximumDropProfile);
        Assert.NotNull(viewModel.LatestLeftMaximumDropProfile);
        Assert.NotNull(viewModel.LatestRightMaximumDropProfile);
        Assert.Equal(PointCloudDeviceSide.Left, viewModel.LatestLeftMaximumDropProfile!.Side);
        Assert.Equal(PointCloudDeviceSide.Right, viewModel.LatestRightMaximumDropProfile!.Side);
        Assert.Single(viewModel.LatestLeftMaximumDropProfileSegments);
        Assert.Single(viewModel.LatestRightMaximumDropProfileSegments);
        Assert.Equal(
            viewModel.LatestRightMaximumDropProfile.ProfilePoints,
            viewModel.LatestMaximumDropProfilePoints);
    }

    private sealed class FakeProfileService : IPointCloudRepresentativeProfileService
    {
        public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
        {
            throw new NotSupportedException();
        }

        public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath, PointCloudDeviceSide side)
        {
            IReadOnlyList<RailProfilePoint> points = side == PointCloudDeviceSide.Left
                ? new[] { new RailProfilePoint(-2.0, 1.0), new RailProfilePoint(-1.0, 2.0) }
                : new[] { new RailProfilePoint(1.0, 2.0), new RailProfilePoint(2.0, 1.0) };

            return new MedianSectionExtractionResult(1.0, points);
        }
    }

    private sealed class FakeSegmentedProfileAnalysisService : IPointCloudProfileAnalysisService
    {
        public PointCloudProfileAnalysisResult AnalyzePoints(
            IReadOnlyList<PointCloudPoint3D> points,
            PointCloudDeviceSide side,
            int sampleIndex = 0)
        {
            throw new NotSupportedException();
        }

        public PointCloudProfileAnalysisResult AnalyzeCsv(
            string csvPath,
            PointCloudDeviceSide side,
            int sampleIndex = 0)
        {
            IReadOnlyList<RailProfilePoint> representativePoints = side == PointCloudDeviceSide.Left
                ? new[] { new RailProfilePoint(-2.0, 1.0), new RailProfilePoint(-1.0, 2.0) }
                : new[] { new RailProfilePoint(1.0, 2.0), new RailProfilePoint(2.0, 1.0) };
            MaximumDropProfileResult? maximumDropProfile = side == PointCloudDeviceSide.Left
                ? new MaximumDropProfileResult(
                    side,
                    sampleIndex,
                    1.0,
                    0.5,
                    new IReadOnlyList<RailProfilePoint>[]
                    {
                        new[]
                        {
                            new RailProfilePoint(-4.0, 0.0),
                            new RailProfilePoint(-3.0, -0.5),
                            new RailProfilePoint(-2.0, 0.0)
                        },
                        new[]
                        {
                            new RailProfilePoint(2.0, 0.0),
                            new RailProfilePoint(3.0, -0.5),
                            new RailProfilePoint(4.0, 0.0)
                        }
                    })
                : null;

            return new PointCloudProfileAnalysisResult(
                new MedianSectionExtractionResult(1.0, representativePoints),
                maximumDropProfile);
        }
    }

    private sealed class FakeDualMaximumDropProfileAnalysisService : IPointCloudProfileAnalysisService
    {
        public PointCloudProfileAnalysisResult AnalyzePoints(
            IReadOnlyList<PointCloudPoint3D> points,
            PointCloudDeviceSide side,
            int sampleIndex = 0)
        {
            throw new NotSupportedException();
        }

        public PointCloudProfileAnalysisResult AnalyzeCsv(
            string csvPath,
            PointCloudDeviceSide side,
            int sampleIndex = 0)
        {
            double startX = side == PointCloudDeviceSide.Left ? -4.0 : 2.0;
            IReadOnlyList<RailProfilePoint> profilePoints = new[]
            {
                new RailProfilePoint(startX, -170.0),
                new RailProfilePoint(startX + 1.0, -171.0),
                new RailProfilePoint(startX + 2.0, -170.0)
            };
            var maximumDropProfile = new MaximumDropProfileResult(
                side,
                sampleIndex,
                side == PointCloudDeviceSide.Left ? 10.0 : 20.0,
                side == PointCloudDeviceSide.Left ? 0.5 : 0.8,
                profilePoints);

            return new PointCloudProfileAnalysisResult(
                new MedianSectionExtractionResult(1.0, profilePoints),
                maximumDropProfile);
        }
    }
}
