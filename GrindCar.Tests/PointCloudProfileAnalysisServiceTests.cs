using System;
using System.Collections.Generic;
using System.IO;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudProfileAnalysisServiceTests
{
    [Fact]
    public void Prepare_FiltersInvalidPointsAndGroupsBySharedYTolerance()
    {
        PointCloudProfilePreprocessor preprocessor = CreatePreprocessor();
        var points = new[]
        {
            new PointCloudPoint3D(0.0, 0.0, 0.0),
            new PointCloudPoint3D(double.NaN, 1.0, 1.0),
            new PointCloudPoint3D(1.0, double.PositiveInfinity, 1.0),
            new PointCloudPoint3D(2.0, 1.00, 3.0),
            new PointCloudPoint3D(1.0, 1.20, 2.0),
            new PointCloudPoint3D(4.0, 1.26, 5.0),
            new PointCloudPoint3D(3.0, 1.26, 4.0)
        };

        PreparedPointCloudProfileData result = preprocessor.Prepare(points);

        Assert.Equal(1.20, result.RepresentativeY, 6);
        Assert.Equal(2, result.Sections.Count);
        Assert.Equal(2, result.Sections[0].Count);
        Assert.Equal(2, result.Sections[1].Count);
        Assert.Equal(1.0, result.Sections[0][0].X, 6);
        Assert.Equal(2.0, result.Sections[0][1].X, 6);
        Assert.Equal(3.0, result.Sections[1][0].X, 6);
        Assert.Equal(4.0, result.Sections[1][1].X, 6);
    }

    [Fact]
    public void AnalyzePoints_PreprocessesOnceForBothAlgorithms()
    {
        CreateAnalysisService(
            out PointCloudProfileAnalysisService analysisService,
            out CountingPreprocessor countingPreprocessor,
            out _);

        PointCloudProfileAnalysisResult result = analysisService.AnalyzePoints(
            BuildPointCloud(),
            PointCloudDeviceSide.Left,
            sampleIndex: 3);

        Assert.Equal(1, countingPreprocessor.CallCount);
        Assert.NotEmpty(result.ExtractionResult.ProfilePoints);
        MaximumDropProfileResult maximumDropProfile =
            Assert.IsType<MaximumDropProfileResult>(result.MaximumDropProfile);
        Assert.Equal(3, maximumDropProfile.SampleIndex);
        Assert.True(maximumDropProfile.MaximumDropDepth > 0.0);
    }

    [Fact]
    public void AnalyzeCsv_ReadsAndPreprocessesOnceForBothAlgorithms()
    {
        CreateAnalysisService(
            out PointCloudProfileAnalysisService analysisService,
            out CountingPreprocessor countingPreprocessor,
            out CsvReadCounter csvReadCounter);

        PointCloudProfileAnalysisResult result = analysisService.AnalyzeCsv(
            "single-read.csv",
            PointCloudDeviceSide.Right,
            sampleIndex: 5);

        Assert.Equal(1, csvReadCounter.CallCount);
        Assert.Equal(1, countingPreprocessor.CallCount);
        Assert.NotEmpty(result.ExtractionResult.ProfilePoints);
        MaximumDropProfileResult maximumDropProfile =
            Assert.IsType<MaximumDropProfileResult>(result.MaximumDropProfile);
        Assert.Equal(5, maximumDropProfile.SampleIndex);
    }

    [Fact]
    public void AnalyzePoints_WithNoDownwardDrop_ReturnsRepresentativeProfileAndNullDropProfile()
    {
        CreateAnalysisService(
            out PointCloudProfileAnalysisService analysisService,
            out _,
            out _);
        List<PointCloudPoint3D> points = BuildPointCloud();
        for (int index = 0; index < points.Count; index++)
        {
            PointCloudPoint3D point = points[index];
            points[index] = new PointCloudPoint3D(
                point.X,
                point.Y,
                StandardRailProfileSolver.RailSurfaceFun(point.X));
        }

        PointCloudProfileAnalysisResult result = analysisService.AnalyzePoints(
            points,
            PointCloudDeviceSide.Left);

        Assert.NotEmpty(result.ExtractionResult.ProfilePoints);
        Assert.Null(result.MaximumDropProfile);
    }

    private static void CreateAnalysisService(
        out PointCloudProfileAnalysisService analysisService,
        out CountingPreprocessor countingPreprocessor,
        out CsvReadCounter csvReadCounter)
    {
        string settingsPath = Path.Combine(
            Path.GetTempPath(),
            "grindcar-tests",
            Guid.NewGuid().ToString("N"),
            "registration.json");
        var settingsStore = new ProfileRegistrationSettingsStore(settingsPath);
        settingsStore.Save(new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(),
            Right = new ProfileRegistrationParameters()
        });
        var transformService = new ProfileRegistrationTransformService();
        var innerPreprocessor = new PointCloudProfilePreprocessor(settingsStore, transformService);
        countingPreprocessor = new CountingPreprocessor(innerPreprocessor);
        var representativeService = new PointCloudRepresentativeProfileService(
            countingPreprocessor,
            transformService);
        var maximumDropService = new MaximumDropProfileService(
            countingPreprocessor,
            transformService);
        csvReadCounter = new CsvReadCounter(BuildPointCloud());
        analysisService = new PointCloudProfileAnalysisService(
            countingPreprocessor,
            representativeService,
            maximumDropService,
            csvReadCounter.Read);
    }

    private static PointCloudProfilePreprocessor CreatePreprocessor()
    {
        string settingsPath = Path.Combine(
            Path.GetTempPath(),
            "grindcar-tests",
            Guid.NewGuid().ToString("N"),
            "registration.json");
        return new PointCloudProfilePreprocessor(
            new ProfileRegistrationSettingsStore(settingsPath),
            new ProfileRegistrationTransformService());
    }

    private static List<PointCloudPoint3D> BuildPointCloud()
    {
        var points = new List<PointCloudPoint3D>();
        for (int sectionIndex = 0; sectionIndex < 5; sectionIndex++)
        {
            double y = sectionIndex + 1.0;
            for (int x = -35; x <= 35; x++)
            {
                double residual = sectionIndex == 3 && x >= -1 && x <= 1 ? -1.5 : 0.0;
                points.Add(new PointCloudPoint3D(
                    x,
                    y,
                    StandardRailProfileSolver.RailSurfaceFun(x) + residual));
            }
        }

        return points;
    }

    private sealed class CountingPreprocessor : IPointCloudProfilePreprocessor
    {
        private readonly IPointCloudProfilePreprocessor _inner;

        public CountingPreprocessor(IPointCloudProfilePreprocessor inner)
        {
            _inner = inner;
        }

        public int CallCount { get; private set; }

        public PreparedPointCloudProfileData Prepare(
            IReadOnlyList<PointCloudPoint3D> points,
            PointCloudDeviceSide? side = null,
            ProfileRegistrationParameters? explicitParameters = null)
        {
            CallCount++;
            return _inner.Prepare(points, side, explicitParameters);
        }
    }

    private sealed class CsvReadCounter
    {
        private readonly List<PointCloudPoint3D> _points;

        public CsvReadCounter(List<PointCloudPoint3D> points)
        {
            _points = points;
        }

        public int CallCount { get; private set; }

        public List<PointCloudPoint3D> Read(string path)
        {
            CallCount++;
            return _points;
        }
    }
}
