using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Debug;
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
}
