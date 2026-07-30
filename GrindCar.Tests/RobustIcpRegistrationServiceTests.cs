using System;
using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using Xunit;

namespace GrindCar.Tests;

public class RobustIcpRegistrationServiceTests
{
    [Fact]
    public void ComposeRigidTransforms_WithRotationAndTranslation_MatchesSequentialApplication()
    {
        const double firstRotationRadians = 20.0 * Math.PI / 180.0;
        const double secondRotationRadians = -7.0 * Math.PI / 180.0;
        RailProfilePoint point = new(2.0, 5.0);

        RailProfilePoint afterFirst = ApplyGlobalTransform(point, 3.0, -2.0, firstRotationRadians);
        RailProfilePoint expected = ApplyGlobalTransform(afterFirst, -1.0, 4.0, secondRotationRadians);
        (double dx, double dy, double rotationRadians) =
            RobustIcpRegistrationService.ComposeRigidTransforms(
                3.0,
                -2.0,
                firstRotationRadians,
                -1.0,
                4.0,
                secondRotationRadians);
        RailProfilePoint actual = ApplyGlobalTransform(point, dx, dy, rotationRadians);

        Assert.Equal(expected.X, actual.X, 10);
        Assert.Equal(expected.Y, actual.Y, 10);
    }

    [Fact]
    public void Align_WithRotatedAndTranslatedAsymmetricProfile_ReturnedTransformReproducesAlignment()
    {
        var service = new RobustIcpRegistrationService();
        IReadOnlyList<RailProfilePoint> targetPoints = BuildAsymmetricTargetPoints();
        IReadOnlyList<RailProfilePoint> sourcePoints = targetPoints
            .Select(point => ApplyGlobalTransform(point, 0.8, -1.2, 1.5 * Math.PI / 180.0))
            .ToArray();

        ProfileRegistrationParameters transform = service.Align(sourcePoints, targetPoints);
        IReadOnlyList<RailProfilePoint> alignedPoints = sourcePoints
            .Select(point => ApplyGlobalTransform(
                point,
                transform.Dx,
                transform.Dy,
                transform.RotationDegrees * Math.PI / 180.0))
            .ToArray();

        double beforeError = CalculateAverageNearestDistance(sourcePoints, targetPoints);
        double afterError = CalculateAverageNearestDistance(alignedPoints, targetPoints);
        Assert.True(afterError < beforeError * 0.2, $"before={beforeError}, after={afterError}");
        Assert.True(afterError < 0.1, $"after={afterError}");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(10, 8)]
    public void ResolveInlierCount_ReturnsExpectedCount(int pointCount, int expectedCount)
    {
        Assert.Equal(expectedCount, RobustIcpRegistrationService.ResolveInlierCount(pointCount));
    }

    private static IReadOnlyList<RailProfilePoint> BuildAsymmetricTargetPoints()
    {
        var points = new List<RailProfilePoint>();
        for (int index = 0; index <= 80; index++)
        {
            double x = -25.0 + index * 0.625;
            double y = 0.015 * x * x + 0.08 * x + 0.4 * Math.Sin(x / 5.0);
            points.Add(new RailProfilePoint(x, y));
        }

        return points;
    }

    private static RailProfilePoint ApplyGlobalTransform(
        RailProfilePoint point,
        double dx,
        double dy,
        double rotationRadians)
    {
        double cosValue = Math.Cos(rotationRadians);
        double sinValue = Math.Sin(rotationRadians);
        return new RailProfilePoint(
            point.X * cosValue - point.Y * sinValue + dx,
            point.X * sinValue + point.Y * cosValue + dy);
    }

    private static double CalculateAverageNearestDistance(
        IReadOnlyList<RailProfilePoint> sourcePoints,
        IReadOnlyList<RailProfilePoint> targetPoints)
    {
        return sourcePoints.Average(sourcePoint =>
        {
            double minimumDistanceSquared = targetPoints.Min(targetPoint =>
                (sourcePoint.X - targetPoint.X) * (sourcePoint.X - targetPoint.X) +
                (sourcePoint.Y - targetPoint.Y) * (sourcePoint.Y - targetPoint.Y));
            return Math.Sqrt(minimumDistanceSquared);
        });
    }
}
