using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
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

        Assert.InRange(error, 0.0, 0.02);
    }

    [Theory]
    [InlineData(PointCloudDeviceSide.Left, false)]
    [InlineData(PointCloudDeviceSide.Left, true)]
    [InlineData(PointCloudDeviceSide.Right, false)]
    [InlineData(PointCloudDeviceSide.Right, true)]
    public void ComposeWithGlobalDelta_WithCurrentParameters_MatchesSequentialTransform(
        PointCloudDeviceSide side,
        bool isMirrored)
    {
        var service = new ProfileRegistrationTransformService();
        var basePoints = new List<RailProfilePoint>
        {
            new(-4.0, 1.0),
            new(-1.0, 5.0),
            new(3.0, 2.0),
            new(6.0, 8.0)
        };
        var currentParameters = new ProfileRegistrationParameters(4.0, -3.0, 12.0, isMirrored)
        {
            XMin = -100.0,
            XMax = 100.0
        };
        var globalDelta = new ProfileRegistrationParameters(1.2, -0.7, 3.0);
        IReadOnlyList<RailProfilePoint> currentPoints = service.Transform(basePoints, currentParameters, side);
        IReadOnlyList<RailProfilePoint> expectedPoints = ApplyGlobalTransform(currentPoints, globalDelta);

        ProfileRegistrationParameters composedParameters = service.ComposeWithGlobalDelta(
            basePoints,
            currentParameters,
            globalDelta,
            side);
        IReadOnlyList<RailProfilePoint> actualPoints = service.Transform(basePoints, composedParameters, side);

        Assert.Equal(expectedPoints.Count, actualPoints.Count);
        for (int index = 0; index < expectedPoints.Count; index++)
        {
            Assert.Equal(expectedPoints[index].X, actualPoints[index].X, 10);
            Assert.Equal(expectedPoints[index].Y, actualPoints[index].Y, 10);
        }

        Assert.Equal(isMirrored, composedParameters.IsMirrored);
        Assert.Equal(currentParameters.XMin, composedParameters.XMin);
        Assert.Equal(currentParameters.XMax, composedParameters.XMax);
    }

    [Fact]
    public void CalculateStandardErrorMetrics_WithTwentyPercentOutliers_UsesIcpInlierRule()
    {
        var service = new ProfileRegistrationTransformService();
        var standardPoints = new List<RailProfilePoint>();
        var measuredPoints = new List<RailProfilePoint>();
        for (int index = 0; index < 8; index++)
        {
            var point = new RailProfilePoint(index, index * 0.25);
            standardPoints.Add(point);
            measuredPoints.Add(point);
        }

        measuredPoints.Add(new RailProfilePoint(100.0, 100.0));
        measuredPoints.Add(new RailProfilePoint(200.0, 200.0));

        ProfileRegistrationErrorMetrics metrics =
            service.CalculateStandardErrorMetrics(measuredPoints, standardPoints);

        Assert.Equal(0.0, metrics.InlierRootMeanSquareDistance, 10);
        Assert.True(metrics.AverageDistance > 0.0);
        Assert.True(metrics.Percentile95Distance > 0.0);
        Assert.Equal(0.8, metrics.InlierRatio, 10);
    }

    [Fact]
    public void ApplyRawOrientation_WithRotation_RotatesXAndZAroundRawCentroidAndKeepsY()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new[]
        {
            new PointCloudPoint3D(10.0, 100.0, 0.0),
            new PointCloudPoint3D(10.0, 200.0, 2.0)
        };
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 90.0);

        IReadOnlyList<PointCloudPoint3D> transformed =
            service.ApplyRawOrientation(points, parameters, PointCloudDeviceSide.Left);

        Assert.Equal(11.0, transformed[0].X, 6);
        Assert.Equal(100.0, transformed[0].Y, 6);
        Assert.Equal(1.0, transformed[0].Z, 6);
        Assert.Equal(9.0, transformed[1].X, 6);
        Assert.Equal(200.0, transformed[1].Y, 6);
        Assert.Equal(1.0, transformed[1].Z, 6);
    }

    [Fact]
    public void ApplyRawOrientation_WithMirrorAndRotation_MirrorsBeforeRotation()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new[]
        {
            new PointCloudPoint3D(10.0, 1.0, 0.0),
            new PointCloudPoint3D(12.0, 2.0, 0.0)
        };
        var parameters = new ProfileRegistrationParameters(0.0, 0.0, 90.0, true);

        IReadOnlyList<PointCloudPoint3D> transformed =
            service.ApplyRawOrientation(points, parameters, PointCloudDeviceSide.Left);

        Assert.Equal(13.0, transformed[0].X, 6);
        Assert.Equal(1.0, transformed[0].Z, 6);
        Assert.Equal(13.0, transformed[1].X, 6);
        Assert.Equal(-1.0, transformed[1].Z, 6);
    }

    [Fact]
    public void ApplyTranslationAndCrop_FiltersByTranslatedX()
    {
        var service = new ProfileRegistrationTransformService();
        var points = new[]
        {
            new RailProfilePoint(1.0, 2.0),
            new RailProfilePoint(2.0, 3.0)
        };
        var parameters = new ProfileRegistrationParameters(3.0, 4.0, 0.0)
        {
            XMin = 5.0,
            XMax = 5.0
        };

        IReadOnlyList<RailProfilePoint> transformed =
            service.ApplyTranslationAndCrop(points, parameters);

        RailProfilePoint point = Assert.Single(transformed);
        Assert.Equal(5.0, point.X, 6);
        Assert.Equal(7.0, point.Y, 6);
    }

    [Fact]
    public void ComposeRawFirstWithGlobalDelta_MatchesSequentialRawFirstTransform()
    {
        var service = new ProfileRegistrationTransformService();
        var rawPoints = new[]
        {
            new PointCloudPoint3D(0.0, 10.0, 0.0),
            new PointCloudPoint3D(2.0, 20.0, 0.0)
        };
        var current = new ProfileRegistrationParameters(3.0, -2.0, 10.0, true)
        {
            XMin = -20.0,
            XMax = 20.0
        };
        var delta = new ProfileRegistrationParameters(-1.0, 4.0, 5.0);

        IReadOnlyList<PointCloudPoint3D> currentOriented =
            service.ApplyRawOrientation(rawPoints, current, PointCloudDeviceSide.Left);
        RailProfilePoint currentPositioned = service.ApplyTranslationAndCrop(
            new[] { new RailProfilePoint(currentOriented[0].X, currentOriented[0].Z) },
            current)[0];
        RailProfilePoint expected = ApplyGlobalTransform(
            new[] { currentPositioned },
            delta)[0];

        ProfileRegistrationParameters composed = service.ComposeRawFirstWithGlobalDelta(
            rawPoints,
            current,
            delta,
            PointCloudDeviceSide.Left);
        IReadOnlyList<PointCloudPoint3D> composedOriented =
            service.ApplyRawOrientation(rawPoints, composed, PointCloudDeviceSide.Left);
        RailProfilePoint actual = service.ApplyTranslationAndCrop(
            new[] { new RailProfilePoint(composedOriented[0].X, composedOriented[0].Z) },
            composed)[0];

        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.True(composed.IsMirrored);
        Assert.Equal(-20.0, composed.XMin);
        Assert.Equal(20.0, composed.XMax);
    }

    private static IReadOnlyList<RailProfilePoint> ApplyGlobalTransform(
        IReadOnlyList<RailProfilePoint> points,
        ProfileRegistrationParameters parameters)
    {
        double radians = parameters.RotationDegrees * Math.PI / 180.0;
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var transformedPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            transformedPoints.Add(new RailProfilePoint(
                point.X * cosValue - point.Y * sinValue + parameters.Dx,
                point.X * sinValue + point.Y * cosValue + parameters.Dy));
        }

        return transformedPoints;
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
