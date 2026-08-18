using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Debug;
using GrindCar.Services.Rail.Processing;
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

    [Fact]
    public void Calculate_WithMaximumDropProfiles_CombinesRegularAndDefectDepths()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "grindcar-tests",
            Guid.NewGuid().ToString("N"));
        string leftCsvPath = Path.Combine(directoryPath, "left.csv");
        string rightCsvPath = Path.Combine(directoryPath, "right.csv");
        PointCloudPointCsvExporter.Export(leftCsvPath, BuildPointCloud(dropDepth: 1.0));
        PointCloudPointCsvExporter.Export(rightCsvPath, BuildPointCloud(dropDepth: 2.0));

        var settingsStore = new ProfileRegistrationSettingsStore(
            Path.Combine(directoryPath, "registration.json"));
        settingsStore.Save(new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(),
            Right = new ProfileRegistrationParameters()
        });
        var maximumDropService = new MaximumDropProfileService(
            settingsStore,
            new ProfileRegistrationTransformService());
        var defectPointSets = new List<IReadOnlyList<RailProfilePoint>>();
        var service = new PointCloudGrindDepthDebugWorkflowService(
            new FakeProfileService(),
            maximumDropService,
            (angles, points) => new GrindDepthCalculationResult(
                angles.Select(angle => new GrindDepthResult(angle, 0.2)).ToArray(),
                points),
            (angles, points) =>
            {
                defectPointSets.Add(points.ToArray());
                double depth = defectPointSets.Count == 1 ? 0.3 : 0.5;
                return angles.Select(angle => new GrindDepthResult(angle, depth)).ToArray();
            });

        PointCloudGrindDepthDebugCalculationOutput output = service.Calculate(
            leftCsvPath,
            rightCsvPath,
            new[] { 0 });

        CombinedGrindDepthResult result = output.Results.Single();
        Assert.Equal(0.2, result.RegularDepth, 6);
        Assert.Equal(0.5, result.DefectDepth, 6);
        Assert.Equal(0.5, result.FinalDepth, 6);
        Assert.NotNull(output.GlobalMaximumDropProfile);
        Assert.Equal(PointCloudDeviceSide.Right, output.GlobalMaximumDropProfile!.Side);
        Assert.Equal(2.0, output.GlobalMaximumDropProfile.MaximumDropDepth, 6);
        Assert.Equal(2, defectPointSets.Count);
        Assert.All(defectPointSets, profilePoints =>
            Assert.Contains(profilePoints, point =>
                point.Y < StandardRailProfileSolver.RailSurfaceFun(point.X)));
    }

    [Fact]
    public void Calculate_WithSharedAnalysis_AnalyzesEachSideOnce()
    {
        var analysisService = new FakePointCloudProfileAnalysisService();
        var service = new PointCloudGrindDepthDebugWorkflowService(
            analysisService,
            (angles, points) => new GrindDepthCalculationResult(
                angles.Select(angle => new GrindDepthResult(angle, 0.2)).ToArray(),
                points),
            (angles, points) => angles
                .Select(angle => new GrindDepthResult(angle, points[0].Y))
                .ToArray());

        PointCloudGrindDepthDebugCalculationOutput output = service.Calculate(
            "left.csv",
            "right.csv",
            new[] { 0 });

        Assert.Equal(new[]
        {
            ("left.csv", PointCloudDeviceSide.Left),
            ("right.csv", PointCloudDeviceSide.Right)
        }, analysisService.Calls);
        Assert.Single(output.Results);
        Assert.Equal(0.5, output.Results[0].DefectDepth, 6);
    }

    [Fact]
    public void Calculate_WithPositiveAndNegativeAngles_UsesOnlyMatchingDropSide()
    {
        var analysisService = new FakePointCloudProfileAnalysisService();
        var defectCalls = new List<(double Marker, int[] Angles)>();
        var service = new PointCloudGrindDepthDebugWorkflowService(
            analysisService,
            (angles, points) => new GrindDepthCalculationResult(
                angles.Select(angle => new GrindDepthResult(angle, 0.1)).ToArray(),
                points),
            (angles, points) =>
            {
                defectCalls.Add((points[0].Y, angles.ToArray()));
                return angles
                    .Select(angle => new GrindDepthResult(angle, points[0].Y))
                    .ToArray();
            });

        PointCloudGrindDepthDebugCalculationOutput output = service.Calculate(
            "left.csv",
            "right.csv",
            new[] { -20, 0, 20 });

        (double Marker, int[] Angles) leftCall = defectCalls.Single(call => call.Marker == 0.3);
        (double Marker, int[] Angles) rightCall = defectCalls.Single(call => call.Marker == 0.5);
        Assert.Equal(new[] { 0, 20 }, leftCall.Angles);
        Assert.Equal(new[] { -20, 0 }, rightCall.Angles);
        Assert.Equal(0.5, output.Results.Single(result => result.Angle == -20).DefectDepth, 6);
        Assert.Equal(0.5, output.Results.Single(result => result.Angle == 0).DefectDepth, 6);
        Assert.Equal(0.3, output.Results.Single(result => result.Angle == 20).DefectDepth, 6);
    }

    private static IReadOnlyList<PointCloudPoint3D> BuildPointCloud(double dropDepth)
    {
        var points = new List<PointCloudPoint3D>();
        for (int x = -35; x <= 35; x++)
        {
            double residual = x >= -1 && x <= 1 ? -dropDepth : 0.0;
            points.Add(new PointCloudPoint3D(
                x,
                1.0,
                StandardRailProfileSolver.RailSurfaceFun(x) + residual));
        }

        return points;
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

    private sealed class FakePointCloudProfileAnalysisService : IPointCloudProfileAnalysisService
    {
        public List<(string Path, PointCloudDeviceSide Side)> Calls { get; } = new();

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
            Calls.Add((csvPath, side));
            double marker = side == PointCloudDeviceSide.Left ? 0.3 : 0.5;
            var representativePoints = new[]
            {
                new RailProfilePoint(side == PointCloudDeviceSide.Left ? -1.0 : 1.0, 0.0)
            };
            var maximumDropPoints = new[] { new RailProfilePoint(0.0, marker) };
            return new PointCloudProfileAnalysisResult(
                new MedianSectionExtractionResult(
                    side == PointCloudDeviceSide.Left ? 10.0 : 20.0,
                    representativePoints),
                new MaximumDropProfileResult(
                    side,
                    sampleIndex,
                    1.0,
                    marker,
                    maximumDropPoints));
        }
    }
}
