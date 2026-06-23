using System;
using System.IO;
using System.Text;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudPointCsvExporterTests
{
    [Fact]
    public void Export_WritesHeaderAndPointCoordinates()
    {
        string outputPath = Path.Combine(CreateTempDirectory(), "points.csv");
        var points = new[]
        {
            new PointCloudPoint3D(1.0, 2.5, -3.25),
            new PointCloudPoint3D(4.1234567, 5.0, 6.0)
        };

        PointCloudPointCsvExporter.Export(outputPath, points);

        string content = File.ReadAllText(outputPath, Encoding.UTF8);
        Assert.Equal(
            "X,Y,Z\r\n1.000000,2.500000,-3.250000\r\n4.123457,5.000000,6.000000\r\n",
            content);
    }

    [Fact]
    public void Export_EmptyPath_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => PointCloudPointCsvExporter.Export(string.Empty, new[] { new PointCloudPoint3D(1.0, 2.0, 3.0) }));

        Assert.Contains("路径不能为空", exception.Message);
    }

    [Fact]
    public void Export_EmptyPoints_Throws()
    {
        string outputPath = Path.Combine(CreateTempDirectory(), "points.csv");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => PointCloudPointCsvExporter.Export(outputPath, Array.Empty<PointCloudPoint3D>()));

        Assert.Contains("点集不能为空", exception.Message);
    }

    private static string CreateTempDirectory()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "GrindCar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
