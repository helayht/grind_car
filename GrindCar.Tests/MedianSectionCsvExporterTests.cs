using System;
using System.IO;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
using Xunit;

namespace GrindCar.Tests;

public class MedianSectionCsvExporterTests
{
    [Fact]
    public void Export_WithMedianAndPoints_WritesExpectedContent()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"));
        string outputPath = Path.Combine(outputDirectory, "median.csv");
        var points = new[]
        {
            new RailProfilePoint(1.0, 2.0),
            new RailProfilePoint(3.0, 4.0)
        };

        MedianSectionCsvExporter.Export(outputPath, 123.456789, points);

        string content = File.ReadAllText(outputPath);
        Assert.Contains("RepresentativeY,123.456789", content);
        Assert.Contains("X,Z", content);
        Assert.Contains("1.000000,2.000000", content);
        Assert.Contains("3.000000,4.000000", content);
    }
}
