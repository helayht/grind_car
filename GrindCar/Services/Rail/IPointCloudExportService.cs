using System.Collections.Generic;
using System;
using GrindCar.Models.PointCloud;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

internal interface IPointCloudExportService
{
    IReadOnlyList<PointCloudPoint3D> CapturePointCloudPoints(
        string serialNumber,
        PointCloudCaptureSettings captureSettings);

    void ExportPointCloud(
        string serialNumber,
        string outputPath,
        PointCloudExportFormat exportFormat,
        PointCloudCaptureSettings captureSettings);
}

internal sealed class PointCloudExportServiceAdapter : IPointCloudExportService
{
    private readonly PointCloudExportService _inner;

    public PointCloudExportServiceAdapter(PointCloudExportService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IReadOnlyList<PointCloudPoint3D> CapturePointCloudPoints(
        string serialNumber,
        PointCloudCaptureSettings captureSettings)
    {
        return _inner.CapturePointCloudPoints(serialNumber, captureSettings);
    }

    public void ExportPointCloud(
        string serialNumber,
        string outputPath,
        PointCloudExportFormat exportFormat,
        PointCloudCaptureSettings captureSettings)
    {
        _inner.ExportPointCloud(serialNumber, outputPath, exportFormat, captureSettings);
    }
}
