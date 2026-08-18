using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudMedianSectionCaptureServiceTests
{
    [Fact]
    public void CaptureMedianSectionProfile_OnlineReturnsEmptyPoints_FallsBackToCsv()
    {
        var exportService = new FakePointCloudExportService(Array.Empty<PointCloudPoint3D>());
        var representativeService = new FakeRepresentativeProfileService();
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            CreateLogDirectoryPath());

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Left,
            new PointCloudCaptureSettings(1.0, 10));

        Assert.True(exportService.ExportCalled);
        Assert.Equal(0, representativeService.PointExtractionCallCount);
        Assert.Equal(1, representativeService.CsvExtractionCallCount);
        Assert.False(string.IsNullOrWhiteSpace(result.CsvPath));
    }

    [Fact]
    public void CaptureMedianSectionProfile_OnlineExtractionFails_FallsBackToCsv()
    {
        var exportService = new FakePointCloudExportService(new[]
        {
            new PointCloudPoint3D(1.0, 1.0, 1.0)
        });
        var representativeService = new FakeRepresentativeProfileService
        {
            ThrowOnPointExtraction = true
        };
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            CreateLogDirectoryPath());

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Left,
            new PointCloudCaptureSettings(1.0, 10));

        Assert.True(exportService.ExportCalled);
        Assert.Equal(1, representativeService.PointExtractionCallCount);
        Assert.Equal(1, representativeService.CsvExtractionCallCount);
        Assert.False(string.IsNullOrWhiteSpace(result.CsvPath));
    }

    [Fact]
    public async Task CaptureMedianSectionProfile_OnlineExtractionSucceeds_ArchivesRawPointCloud()
    {
        var exportService = new FakePointCloudExportService(new[]
        {
            new PointCloudPoint3D(1.0, 2.0, 3.0)
        });
        var representativeService = new FakeRepresentativeProfileService();
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            CreateLogDirectoryPath());
        var progressSource = new TaskCompletionSource<string>();
        var progress = new Progress<string>(message => progressSource.TrySetResult(message));
        var archiveContext = new MeasurementPointCloudArchiveContext(
            1,
            1,
            "SN-001",
            PointCloudDeviceSide.Left,
            progress);

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Left,
            new PointCloudCaptureSettings(1.0, 10),
            archiveContext);

        Task completedTask = await Task.WhenAny(progressSource.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(progressSource.Task, completedTask);
        string progressMessage = await progressSource.Task;
        string outputPath = progressMessage["点云留档完成：".Length..];
        Assert.True(File.Exists(outputPath));
        Assert.Equal(string.Empty, result.CsvPath);
        Assert.Equal(1, representativeService.PointExtractionCallCount);
        Assert.Equal(0, representativeService.CsvExtractionCallCount);
    }

    [Fact]
    public void CaptureMedianSectionProfile_OnlineExtraction_ReturnsMaximumDropProfile()
    {
        var points = new List<PointCloudPoint3D>();
        for (int x = -35; x <= 35; x++)
        {
            double residual = x >= -1 && x <= 1 ? -1.5 : 0.0;
            points.Add(new PointCloudPoint3D(
                x,
                1.0,
                StandardRailProfileSolver.RailSurfaceFun(x) + residual));
        }

        var exportService = new FakePointCloudExportService(points);
        var representativeService = new FakeRepresentativeProfileService();
        MaximumDropProfileService maximumDropProfileService = CreateMaximumDropProfileService();
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            maximumDropProfileService,
            CreateLogDirectoryPath());

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Left,
            new PointCloudCaptureSettings(1.0, 10),
            new MeasurementPointCloudArchiveContext(
                3,
                1,
                "SN-001",
                PointCloudDeviceSide.Left,
                null));

        Assert.NotNull(result.MaximumDropProfile);
        Assert.Equal(3, result.MaximumDropProfile!.SampleIndex);
        Assert.Equal(1.5, result.MaximumDropProfile.MaximumDropDepth, 6);
    }

    [Fact]
    public void CaptureMedianSectionProfile_SharedOnlineAnalysisRunsOnce()
    {
        var exportService = new FakePointCloudExportService(new[]
        {
            new PointCloudPoint3D(1.0, 2.0, 3.0)
        });
        var representativeService = new FakeRepresentativeProfileService();
        var analysisService = new FakePointCloudProfileAnalysisService();
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            null,
            CreateLogDirectoryPath(),
            analysisService);

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Left,
            new PointCloudCaptureSettings(1.0, 10));

        Assert.Equal(1, analysisService.PointAnalysisCallCount);
        Assert.Equal(0, analysisService.CsvAnalysisCallCount);
        Assert.Equal(0, representativeService.PointExtractionCallCount);
        Assert.Equal(string.Empty, result.CsvPath);
        Assert.NotNull(result.MaximumDropProfile);
    }

    [Fact]
    public void CaptureMedianSectionProfile_SharedOnlineAnalysisWithoutDropDoesNotFallBackToCsv()
    {
        var exportService = new FakePointCloudExportService(new[]
        {
            new PointCloudPoint3D(1.0, 2.0, 3.0)
        });
        var representativeService = new FakeRepresentativeProfileService();
        var analysisService = new FakePointCloudProfileAnalysisService
        {
            ReturnNoDropProfile = true
        };
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            null,
            CreateLogDirectoryPath(),
            analysisService);

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Left,
            new PointCloudCaptureSettings(1.0, 10));

        Assert.Equal(1, analysisService.PointAnalysisCallCount);
        Assert.Equal(0, analysisService.CsvAnalysisCallCount);
        Assert.False(exportService.ExportCalled);
        Assert.Null(result.MaximumDropProfile);
    }

    [Fact]
    public void CaptureMedianSectionProfile_SharedCsvFallbackAnalyzesOnce()
    {
        var exportService = new FakePointCloudExportService(Array.Empty<PointCloudPoint3D>());
        var representativeService = new FakeRepresentativeProfileService();
        var analysisService = new FakePointCloudProfileAnalysisService();
        var service = new PointCloudMedianSectionCaptureService(
            exportService,
            representativeService,
            null,
            CreateLogDirectoryPath(),
            analysisService);

        PointCloudMedianSectionCaptureResult result = service.CaptureMedianSectionProfile(
            "SN-001",
            PointCloudDeviceSide.Right,
            new PointCloudCaptureSettings(1.0, 10));

        Assert.True(exportService.ExportCalled);
        Assert.Equal(0, analysisService.PointAnalysisCallCount);
        Assert.Equal(1, analysisService.CsvAnalysisCallCount);
        Assert.Equal(0, representativeService.CsvExtractionCallCount);
        Assert.False(string.IsNullOrWhiteSpace(result.CsvPath));
        Assert.NotNull(result.MaximumDropProfile);
    }

    private static string CreateLogDirectoryPath()
    {
        return System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GrindCar.Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static MaximumDropProfileService CreateMaximumDropProfileService()
    {
        string settingsPath = Path.Combine(
            Path.GetTempPath(),
            "GrindCar.Tests",
            Guid.NewGuid().ToString("N"),
            "point-cloud-profile-registration.json");
        var settingsStore = new ProfileRegistrationSettingsStore(settingsPath);
        settingsStore.Save(new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(),
            Right = new ProfileRegistrationParameters()
        });
        return new MaximumDropProfileService(
            settingsStore,
            new ProfileRegistrationTransformService());
    }

    private sealed class FakePointCloudExportService : IPointCloudExportService
    {
        private readonly IReadOnlyList<PointCloudPoint3D> _points;

        public FakePointCloudExportService(IReadOnlyList<PointCloudPoint3D> points)
        {
            _points = points;
        }

        public bool ExportCalled { get; private set; }

        public IReadOnlyList<PointCloudPoint3D> CapturePointCloudPoints(
            string serialNumber,
            PointCloudCaptureSettings captureSettings)
        {
            return _points;
        }

        public void ExportPointCloud(
            string serialNumber,
            string outputPath,
            PointCloudExportFormat exportFormat,
            PointCloudCaptureSettings captureSettings)
        {
            ExportCalled = true;
        }
    }

    private sealed class FakeRepresentativeProfileService :
        IPointCloudRepresentativeProfileService,
        IPointCloudRepresentativeProfilePointExtractor
    {
        private readonly MedianSectionExtractionResult _result =
            new(1.0, new[] { new RailProfilePoint(1.0, 1.0) });

        public bool ThrowOnPointExtraction { get; init; }

        public int PointExtractionCallCount { get; private set; }

        public int CsvExtractionCallCount { get; private set; }

        public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
        {
            CsvExtractionCallCount++;
            return _result;
        }

        public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(
            string csvPath,
            PointCloudDeviceSide side)
        {
            CsvExtractionCallCount++;
            return _result;
        }

        public MedianSectionExtractionResult ExtractMedianSectionProfileFromPoints(
            IReadOnlyList<PointCloudPoint3D> points,
            PointCloudDeviceSide side)
        {
            PointExtractionCallCount++;
            if (ThrowOnPointExtraction)
            {
                throw new RepresentativeProfileExtractionException("在线提取失败。");
            }

            return _result;
        }
    }

    private sealed class FakePointCloudProfileAnalysisService : IPointCloudProfileAnalysisService
    {
        public bool ReturnNoDropProfile { get; init; }

        public int PointAnalysisCallCount { get; private set; }

        public int CsvAnalysisCallCount { get; private set; }

        public PointCloudProfileAnalysisResult AnalyzePoints(
            IReadOnlyList<PointCloudPoint3D> points,
            PointCloudDeviceSide side,
            int sampleIndex = 0)
        {
            PointAnalysisCallCount++;
            return CreateResult(side, sampleIndex);
        }

        public PointCloudProfileAnalysisResult AnalyzeCsv(
            string csvPath,
            PointCloudDeviceSide side,
            int sampleIndex = 0)
        {
            CsvAnalysisCallCount++;
            return CreateResult(side, sampleIndex);
        }

        private PointCloudProfileAnalysisResult CreateResult(
            PointCloudDeviceSide side,
            int sampleIndex)
        {
            var profilePoints = new[] { new RailProfilePoint(1.0, 1.0) };
            return new PointCloudProfileAnalysisResult(
                new MedianSectionExtractionResult(1.0, profilePoints),
                ReturnNoDropProfile
                    ? null
                    : new MaximumDropProfileResult(side, sampleIndex, 1.0, 0.5, profilePoints));
        }
    }
}
