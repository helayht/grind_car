using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
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
            new(2.5, 20.0, 30.0),
            new(0.0, 30.0, 0.0),
            new(1.0, 30.0, 10.0),
            new(2.0, 30.0, 20.0),
            new(0.5, 40.0, 10.0),
            new(1.5, 40.0, 20.0),
            new(2.5, 40.0, 30.0),
            new(0.0, 50.0, 0.0),
            new(1.0, 50.0, 10.0),
            new(2.0, 50.0, 20.0)
        };
        var service = new PointCloudRepresentativeProfileService();

        var result = service.ExtractMedianSectionProfileFromPoints(points);

        Assert.Equal(30.0, result.RepresentativeY, 6);
        Assert.Equal(3, result.ProfilePoints.Count);
        Assert.Equal(5.0, result.ProfilePoints.Single(point => point.X == 0.5).Y, 6);
        Assert.Equal(15.0, result.ProfilePoints.Single(point => point.X == 1.5).Y, 6);
        Assert.Equal(20.0, result.ProfilePoints.Single(point => point.X == 2.0).Y, 6);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithOutlierZValues_AveragesAfterRemovingOutlier()
    {
        var points = CreateFlatSectionsAtZ(
            (10.0, 10.0),
            (20.0, 10.1),
            (30.0, 9.9),
            (40.0, 10.0),
            (50.0, 50.0));
        var service = new PointCloudRepresentativeProfileService();

        var result = service.ExtractMedianSectionProfileFromPoints(points);

        Assert.Equal(10.0, result.ProfilePoints.Single(point => point.X == 1.0).Y, 6);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithMinimumZValues_AppliesOutlierFilter()
    {
        var points = CreateFlatSectionsAtZ(
            (10.0, 10.0),
            (20.0, 10.0),
            (30.0, 10.0),
            (40.0, 50.0),
            (50.0, 50.0));
        var service = new PointCloudRepresentativeProfileService();

        var result = service.ExtractMedianSectionProfileFromPoints(points);

        Assert.Equal(10.0, result.ProfilePoints.Single(point => point.X == 1.0).Y, 6);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithOutlierAtMaxCommonX_AveragesAfterRemovingOutlier()
    {
        var points = new List<PointCloudPoint3D>();
        AddThreePointSection(points, 10.0, 10.0);
        AddThreePointSection(points, 20.0, 10.0);
        AddThreePointSection(points, 30.0, 10.2);
        AddThreePointSection(points, 40.0, 9.8);
        AddThreePointSection(points, 50.0, 70.0);
        var service = new PointCloudRepresentativeProfileService();

        var result = service.ExtractMedianSectionProfileFromPoints(points);

        Assert.Equal(10.0, result.ProfilePoints.Single(point => point.X == 1.0).Y, 6);
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
            new(3.0, 20.0, 4.0),
            new(4.0, 30.0, 5.0),
            new(5.0, 30.0, 6.0),
            new(6.0, 40.0, 7.0),
            new(7.0, 40.0, 8.0),
            new(8.0, 50.0, 9.0),
            new(9.0, 50.0, 10.0)
        };
        var service = new PointCloudRepresentativeProfileService();

        RepresentativeProfileExtractionException exception = Assert.Throws<RepresentativeProfileExtractionException>(() =>
            service.ExtractMedianSectionProfileFromPoints(points));

        Assert.Contains("没有公共 X 范围", exception.Message);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithSideAndMissingRegistration_Throws()
    {
        var service = new PointCloudRepresentativeProfileService(
            new ProfileRegistrationSettingsStore(BuildTempRegistrationPath()));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.ExtractMedianSectionProfileFromPoints(CreateFiveSections(), PointCloudDeviceSide.Left));

        Assert.Contains("请先完成代表廓形手动配准并保存参数", exception.Message);
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithSide_AppliesSavedRegistration()
    {
        string registrationPath = BuildTempRegistrationPath();
        var store = new ProfileRegistrationSettingsStore(registrationPath);
        store.Save(new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(1.0, 2.0, 0.0),
            Right = new ProfileRegistrationParameters(0.0, 0.0, 0.0)
        });
        var service = new PointCloudRepresentativeProfileService(store);

        var result = service.ExtractMedianSectionProfileFromPoints(CreateFiveSections(), PointCloudDeviceSide.Left);

        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 2.0) && NearlyEqual(point.Y, 12.0));
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 3.0) && NearlyEqual(point.Y, 22.0));
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 4.0) && NearlyEqual(point.Y, 32.0));
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithSideAndMirror_AppliesSavedMirrorBeforeRegistration()
    {
        string registrationPath = BuildTempRegistrationPath();
        var store = new ProfileRegistrationSettingsStore(registrationPath);
        store.Save(new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(1.0, 2.0, 0.0, true),
            Right = new ProfileRegistrationParameters(0.0, 0.0, 0.0)
        });
        var service = new PointCloudRepresentativeProfileService(store);

        var result = service.ExtractMedianSectionProfileFromPoints(CreateFiveSections(), PointCloudDeviceSide.Left);

        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 6.0) && NearlyEqual(point.Y, 12.0));
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 5.0) && NearlyEqual(point.Y, 22.0));
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 4.0) && NearlyEqual(point.Y, 32.0));
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithRotation_RotatesRawPointsBeforeAverage()
    {
        List<PointCloudPoint3D> points = CreateVerticalSections();
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 90.0);
        var service = new PointCloudRepresentativeProfileService();

        MedianSectionExtractionResult result = service.ExtractMedianSectionProfileFromPoints(
            points,
            PointCloudDeviceSide.Left,
            parameters);

        Assert.Equal(3, result.ProfilePoints.Count);
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 9.0) && NearlyEqual(point.Y, 1.0));
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 10.0) && NearlyEqual(point.Y, 1.0));
        Assert.Contains(result.ProfilePoints, point => NearlyEqual(point.X, 11.0) && NearlyEqual(point.Y, 1.0));
    }

    [Fact]
    public void ExtractMedianSectionProfileFromPoints_WithTranslationAndCrop_AppliesPositionAfterAverage()
    {
        List<PointCloudPoint3D> points = CreateVerticalSections();
        var parameters = new ProfileRegistrationParameters(2.0, 3.0, 90.0)
        {
            XMin = 12.0,
            XMax = 12.5
        };
        var service = new PointCloudRepresentativeProfileService();

        MedianSectionExtractionResult result = service.ExtractMedianSectionProfileFromPoints(
            points,
            PointCloudDeviceSide.Left,
            parameters);

        RailProfilePoint point = Assert.Single(result.ProfilePoints);
        Assert.Equal(12.0, point.X, 6);
        Assert.Equal(4.0, point.Y, 6);
    }

    private static List<PointCloudPoint3D> CreateFlatSectionsAtZ(params (double Y, double Z)[] sections)
    {
        var points = new List<PointCloudPoint3D>();
        for (int index = 0; index < sections.Length; index++)
        {
            (double y, double z) = sections[index];
            points.Add(new PointCloudPoint3D(0.0, y, z));
            points.Add(new PointCloudPoint3D(1.0, y, z));
            points.Add(new PointCloudPoint3D(2.0, y, z));
        }

        return points;
    }

    private static void AddThreePointSection(ICollection<PointCloudPoint3D> points, double y, double maxCommonZ)
    {
        points.Add(new PointCloudPoint3D(0.0, y, 10.0));
        points.Add(new PointCloudPoint3D(0.6, y, 10.0));
        points.Add(new PointCloudPoint3D(1.0, y, maxCommonZ));
    }

    private static List<PointCloudPoint3D> CreateFiveSections()
    {
        var points = new List<PointCloudPoint3D>
        {
            new(1.0, 10.0, 10.0),
            new(2.0, 10.0, 20.0),
            new(3.0, 10.0, 30.0),
            new(1.0, 20.0, 10.0),
            new(2.0, 20.0, 20.0),
            new(3.0, 20.0, 30.0),
            new(1.0, 30.0, 10.0),
            new(2.0, 30.0, 20.0),
            new(3.0, 30.0, 30.0),
            new(1.0, 40.0, 10.0),
            new(2.0, 40.0, 20.0),
            new(3.0, 40.0, 30.0),
            new(1.0, 50.0, 10.0),
            new(2.0, 50.0, 20.0),
            new(3.0, 50.0, 30.0)
        };

        return points;
    }

    private static List<PointCloudPoint3D> CreateVerticalSections()
    {
        var points = new List<PointCloudPoint3D>();
        for (int sectionIndex = 0; sectionIndex < 5; sectionIndex++)
        {
            double sectionY = 10.0 + sectionIndex * 10.0;
            points.Add(new PointCloudPoint3D(10.0, sectionY, 0.0));
            points.Add(new PointCloudPoint3D(10.0, sectionY, 1.0));
            points.Add(new PointCloudPoint3D(10.0, sectionY, 2.0));
        }

        return points;
    }

    private static string BuildTempRegistrationPath()
    {
        return Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "point-cloud-profile-registration.json");
    }

    private static bool NearlyEqual(double left, double right)
    {
        return Math.Abs(left - right) < 1e-6;
    }
}
