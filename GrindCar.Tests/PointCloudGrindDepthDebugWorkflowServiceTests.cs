using System;
using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Debug;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudGrindDepthDebugWorkflowServiceTests
{
    [Fact]
    public void Calculate_WithLeftAndRightCsv_ExtractsBothSidesAndCalculatesDepth()
    {
        var profileService = new FakeProfileService();
        var calculatorAngles = new List<int>();
        var calculatorPoints = new List<RailProfilePoint>();
        var service = new PointCloudGrindDepthDebugWorkflowService(
            profileService,
            (angles, points) =>
            {
                calculatorAngles.AddRange(angles);
                calculatorPoints.AddRange(points);
                return new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, angle / 10.0)).ToArray(),
                    points);
            });

        PointCloudGrindDepthDebugCalculationOutput output =
            service.Calculate("left.csv", "right.csv", new[] { -5, 10 });

        Assert.Equal(new[]
        {
            ("left.csv", PointCloudDeviceSide.Left),
            ("right.csv", PointCloudDeviceSide.Right)
        }, profileService.SideCalls);
        Assert.Equal(new[] { -5, 10 }, calculatorAngles);
        Assert.Equal(4, calculatorPoints.Count);
        Assert.Equal(4, output.RepresentativePoints.Count);
        Assert.Equal(2, output.Results.Count);
        Assert.Equal(2, output.LeftPointCount);
        Assert.Equal(2, output.RightPointCount);
    }

    [Fact]
    public void Calculate_WithEmptyAngles_ReturnsEmptyResultsAndMergedRepresentativePoints()
    {
        var service = new PointCloudGrindDepthDebugWorkflowService(
            new FakeProfileService(),
            RailSurfaceService.CalculateGrindDepths);

        PointCloudGrindDepthDebugCalculationOutput output =
            service.Calculate("left.csv", "right.csv", Array.Empty<int>());

        Assert.Empty(output.Results);
        Assert.Equal(4, output.RepresentativePoints.Count);
    }

    [Fact]
    public void Calculate_WithoutLeftCsv_Throws()
    {
        var service = new PointCloudGrindDepthDebugWorkflowService(
            new FakeProfileService(),
            RailSurfaceService.CalculateGrindDepths);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.Calculate(string.Empty, "right.csv", new[] { 0 }));

        Assert.Contains("Left", exception.Message);
    }

    [Fact]
    public void Calculate_WithRightExtractionFailure_ReportsSideAndRootCause()
    {
        var service = new PointCloudGrindDepthDebugWorkflowService(
            new FakeProfileService(failingSide: PointCloudDeviceSide.Right),
            RailSurfaceService.CalculateGrindDepths);

        RepresentativeProfileExtractionException exception =
            Assert.Throws<RepresentativeProfileExtractionException>(() =>
                service.Calculate("left.csv", "right.csv", new[] { 0 }));

        Assert.Contains("Right", exception.Message);
        Assert.Contains("旋转对齐失败", exception.Message);
    }

    private sealed class FakeProfileService : IPointCloudRepresentativeProfileService
    {
        private readonly PointCloudDeviceSide? _failingSide;

        public FakeProfileService(PointCloudDeviceSide? failingSide = null)
        {
            _failingSide = failingSide;
        }

        public List<(string Path, PointCloudDeviceSide Side)> SideCalls { get; } = new();

        public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
        {
            throw new NotSupportedException();
        }

        public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath, PointCloudDeviceSide side)
        {
            SideCalls.Add((csvPath, side));
            if (_failingSide == side)
            {
                throw new RepresentativeProfileExtractionException(
                    "从 CSV 提取并对齐平均代表截面时发生未处理异常。",
                    new InvalidOperationException("旋转对齐失败，未找到满足垂直边界和工作面贴合要求的候选结果。"));
            }

            IReadOnlyList<RailProfilePoint> points = side == PointCloudDeviceSide.Left
                ? new[] { new RailProfilePoint(-2.0, 1.0), new RailProfilePoint(-1.0, 2.0) }
                : new[] { new RailProfilePoint(1.0, 2.0), new RailProfilePoint(2.0, 1.0) };

            return new MedianSectionExtractionResult(
                side == PointCloudDeviceSide.Left ? 10.0 : 20.0,
                points);
        }
    }
}
