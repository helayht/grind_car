using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using Xunit;

namespace GrindCar.Tests;

public class ProfileRegistrationTransformServiceTests
{
    [Fact]
    public void Transform_WithTranslation_AppliesDxAndDy()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint> { new(1.0, 2.0) };
        var parameters = new ProfileRegistrationParameters(3.0, -4.0, 0.0);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(points, parameters);

        Assert.Equal(4.0, transformed[0].X, 6);
        Assert.Equal(-2.0, transformed[0].Y, 6);
    }

    [Fact]
    public void Transform_WithRotation_AppliesCounterClockwiseRotation()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(0.0, 0.0),
            new(2.0, 0.0)
        };
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 90.0);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(points, parameters);

        Assert.Equal(1.0, transformed[0].X, 6);
        Assert.Equal(-1.0, transformed[0].Y, 6);
        Assert.Equal(1.0, transformed[1].X, 6);
        Assert.Equal(1.0, transformed[1].Y, 6);
    }

    [Fact]
    public void Transform_WithRotationAndTranslation_AppliesFormulaInExpectedOrder()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(0.0, 0.0),
            new(2.0, 0.0)
        };
        var parameters = new ProfileRegistrationParameters(5.0, -1.0, 90.0);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(points, parameters);

        Assert.Equal(6.0, transformed[0].X, 6);
        Assert.Equal(-2.0, transformed[0].Y, 6);
        Assert.Equal(6.0, transformed[1].X, 6);
        Assert.Equal(0.0, transformed[1].Y, 6);
    }

    [Fact]
    public void Transform_WithRotationAndTranslation_MovesCentroidOnlyByTranslation()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(-2.0, 10.0),
            new(2.0, 10.0),
            new(0.0, 16.0)
        };
        var parameters = new ProfileRegistrationParameters(3.0, -5.0, 37.0);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(points, parameters);

        Assert.Equal(3.0, AverageX(transformed) - AverageX(points), 6);
        Assert.Equal(-5.0, AverageY(transformed) - AverageY(points), 6);
    }

    [Fact]
    public void Transform_WithLeftMirror_MirrorsAcrossMaxXBeforeOtherTransforms()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(1.0, 0.0),
            new(3.0, 2.0)
        };
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 0.0, true);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(
            points,
            parameters,
            PointCloudDeviceSide.Left);

        Assert.Equal(5.0, transformed[0].X, 6);
        Assert.Equal(0.0, transformed[0].Y, 6);
        Assert.Equal(3.0, transformed[1].X, 6);
        Assert.Equal(2.0, transformed[1].Y, 6);
    }

    [Fact]
    public void Transform_WithRightMirror_MirrorsAcrossMinXBeforeOtherTransforms()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(1.0, 0.0),
            new(3.0, 2.0)
        };
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 0.0, true);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(
            points,
            parameters,
            PointCloudDeviceSide.Right);

        Assert.Equal(1.0, transformed[0].X, 6);
        Assert.Equal(0.0, transformed[0].Y, 6);
        Assert.Equal(-1.0, transformed[1].X, 6);
        Assert.Equal(2.0, transformed[1].Y, 6);
    }

    [Fact]
    public void Transform_WithoutMirror_KeepsOriginalXDirection()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(1.0, 0.0),
            new(3.0, 2.0)
        };
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 0.0, false);

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(
            points,
            parameters,
            PointCloudDeviceSide.Right);

        Assert.Equal(1.0, transformed[0].X, 6);
        Assert.Equal(0.0, transformed[0].Y, 6);
        Assert.Equal(3.0, transformed[1].X, 6);
        Assert.Equal(2.0, transformed[1].Y, 6);
    }

    [Fact]
    public void Transform_WithEmptyPoints_ReturnsEmpty()
    {
        var service = new ProfileRegistrationTransformService();

        IReadOnlyList<RailProfilePoint> transformed = service.Transform(
            Array.Empty<RailProfilePoint>(),
            new ProfileRegistrationParameters(1.0, 2.0, 3.0));

        Assert.Empty(transformed);
    }

    [Fact]
    public void CalculateAverageAbsoluteStandardError_WithStandardPoints_ReturnsZero()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new List<RailProfilePoint>
        {
            new(-10.0, StandardRailProfileSolver.RailSurfaceFun(-10.0)),
            new(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0)),
            new(10.0, StandardRailProfileSolver.RailSurfaceFun(10.0))
        };

        double error = service.CalculateAverageAbsoluteStandardError(points);

        Assert.Equal(0.0, error, 6);
    }

    private static double AverageX(IReadOnlyList<RailProfilePoint> points)
    {
        double sum = 0.0;
        for (int index = 0; index < points.Count; index++)
        {
            sum += points[index].X;
        }

        return sum / points.Count;
    }

    private static double AverageY(IReadOnlyList<RailProfilePoint> points)
    {
        double sum = 0.0;
        for (int index = 0; index < points.Count; index++)
        {
            sum += points[index].Y;
        }

        return sum / points.Count;
    }
}
