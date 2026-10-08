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
    public void ToMeasurementResult_SourceChanges_PreservesSnapshotAndRoundsUpTimes()
    {
        var points = new List<RailProfilePoint> { new(0, 1), new(1, 2) };
        var results = new List<CombinedGrindDepthResult> { new(0, 0.02, 0.051, 0.051) };
        var output = new PointCloudGrindDepthDebugCalculationOutput(
            0, 1, 0, 1, results, points, null, null);

        var measurementResult = output.ToMeasurementResult();
        points.Clear();
        results.Clear();

        Assert.Equal(2, Assert.Single(measurementResult.RepresentativeProfiles).Points.Count);
        Assert.Equal(2, Assert.Single(measurementResult.Results).GrindingTimes);
        Assert.Equal(0.051, measurementResult.Results[0].FinalGrindDepth);
    }

    [Fact]
    public async Task CalculateAsync_WhenCalculationFails_ReleasesBusyStateAndClearsResults()
    {
        var viewModel = new PointCloudGrindDepthDebugViewModel(
            new PointCloudGrindDepthDebugWorkflowService(new FakeProfileService(),
                (angles, points) => throw new InvalidOperationException("模拟计算失败")));
        viewModel.SetLeftFilePath("left.csv");
        viewModel.SetRightFilePath("right.csv");
        viewModel.AnglesInput = "0";

        await Assert.ThrowsAnyAsync<Exception>(() => viewModel.CalculateAsync());

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CanCalculate);
        Assert.Empty(viewModel.Results);
        Assert.Empty(viewModel.LatestRepresentativePoints);
    }

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

        PointCloudGrindDepthDebugCalculationOutput output = await viewModel.CalculateAsync();

        Assert.Equal(2, viewModel.Results.Count);
        Assert.Equal(4, output.RepresentativePoints.Count);
        var measurementResult = output.ToMeasurementResult();
        Assert.Equal(1, measurementResult.SampleCount);
        Assert.Null(measurementResult.MaximumDropProfile);
        Assert.Equal(output.RepresentativePoints, Assert.Single(measurementResult.RepresentativeProfiles).Points);
        Assert.Equal(1, measurementResult.RepresentativeProfiles[0].SampleIndex);
        Assert.Equal(output.Results.Count, measurementResult.Results.Count);
        for (int index = 0; index < output.Results.Count; index++)
        {
            Assert.Equal(output.Results[index].Angle, measurementResult.Results[index].Angle);
            Assert.Equal(output.Results[index].RegularDepth, measurementResult.Results[index].RegularAverageDepth);
            Assert.Equal(output.Results[index].DefectDepth, measurementResult.Results[index].DefectDepth);
            Assert.Equal(output.Results[index].FinalDepth, measurementResult.Results[index].FinalGrindDepth);
        }
        Assert.Equal(0, measurementResult.Results[0].GrindingTimes);
        Assert.Equal(2, measurementResult.Results[1].GrindingTimes);
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

        PointCloudGrindDepthDebugCalculationOutput output = await viewModel.CalculateAsync();
        var measurementResult = output.ToMeasurementResult();
        Assert.Same(output.RightMaximumDropProfile, measurementResult.MaximumDropProfile);
        Assert.Equal(0.2, measurementResult.Results[0].RegularAverageDepth);
        Assert.Equal(0.5, measurementResult.Results[0].DefectDepth);
        Assert.Equal(0.5, measurementResult.Results[0].FinalGrindDepth);
        Assert.Equal(10, measurementResult.Results[0].GrindingTimes);

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
