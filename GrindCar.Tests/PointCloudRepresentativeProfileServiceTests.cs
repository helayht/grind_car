using System.Collections.Generic;
using System.Linq;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudRepresentativeProfileServiceTests
{
    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithDifferentXSections_ReturnsInterpolatedAverageSection()
    {
        var points = new List<PointCloudPoint3D>
        {
            new(0.0, 0.0, 0.0),
            new(0.0, 10.0, 0.0),
            new(1.0, 10.0, 10.0),
            new(2.0, 10.0, 20.0),
            new(0.5, 20.0, 10.0),
            new(1.5, 20.0, 20.0),
            new(2.5, 20.0, 30.0)
        };
        var service = new PointCloudRepresentativeProfileService();

        var result = service.ExtractMedianSectionProfileFromPoints(points);

        Assert.Equal(15.0, result.RepresentativeY, 6);
        Assert.Equal(3, result.ProfilePoints.Count);
        Assert.Equal(7.5, result.ProfilePoints.Single(point => point.X == 0.5).Y, 6);
        Assert.Equal(17.5, result.ProfilePoints.Single(point => point.X == 1.5).Y, 6);
        Assert.Equal(22.5, result.ProfilePoints.Single(point => point.X == 2.0).Y, 6);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithOnlyZeroPoints_Throws()
    {
        var points = new List<PointCloudPoint3D>
        {
            new(0.0, 0.0, 0.0),
            new(0.0, 0.0, 0.0)
        };
        var service = new PointCloudRepresentativeProfileService();

        RepresentativeProfileExtractionException exception = Assert.Throws<RepresentativeProfileExtractionException>(() =>
            service.ExtractMedianSectionProfileFromPoints(points));

        Assert.Contains("未解析到有效坐标点", exception.Message);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithoutCommonXRange_Throws()
    {
        var points = new List<PointCloudPoint3D>
        {
            new(0.0, 10.0, 1.0),
            new(1.0, 10.0, 2.0),
            new(2.0, 20.0, 3.0),
            new(3.0, 20.0, 4.0)
        };
        var service = new PointCloudRepresentativeProfileService();

        RepresentativeProfileExtractionException exception = Assert.Throws<RepresentativeProfileExtractionException>(() =>
            service.ExtractMedianSectionProfileFromPoints(points));

        Assert.Contains("没有公共 X 范围", exception.Message);
    }
}
