using System;
using System.IO;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
using Xunit;

namespace GrindCar.Tests;

public class RepresentativePointsCsvExporterTests
{
    [Fact]
    public void Export_WithPoints_WritesCsvHeaderAndRows()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"));
        string outputPath = Path.Combine(outputDirectory, "points.csv");
        var points = new[]
        {
            new RailProfilePoint(1.23, 4.56),
            new RailProfilePoint(-7.89, 0.12)
        };

        RepresentativePointsCsvExporter.Export(outputPath, points);

        string content = File.ReadAllText(outputPath);
        Assert.Contains("X,Y", content);
        Assert.Contains("1.230000,4.560000", content);
        Assert.Contains("-7.890000,0.120000", content);
    }

    [Fact]
    public void Export_WithoutPoints_Throws()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "grindcar-tests", "empty.csv");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepresentativePointsCsvExporter.Export(outputPath, Array.Empty<RailProfilePoint>()));

        Assert.Contains("没有可导出的代表点", exception.Message);
    }
}