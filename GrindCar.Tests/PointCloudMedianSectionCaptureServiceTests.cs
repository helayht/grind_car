using System;
using System.Collections.Generic;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
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

    private static string CreateLogDirectoryPath()
    {
        return System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GrindCar.Tests",
            Guid.NewGuid().ToString("N"));
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
}
