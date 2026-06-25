using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 代表廓形二维刚体配准变换。
/// </summary>
public sealed class ProfileRegistrationTransformService
{
    private const double DegreesToRadiansFactor = Math.PI / 180.0;

    public IReadOnlyList<RailProfilePoint> Transform(
        IReadOnlyList<RailProfilePoint> points,
        ProfileRegistrationParameters parameters)
    {
        return Transform(points, parameters, PointCloudDeviceSide.Left);
    }

    public IReadOnlyList<RailProfilePoint> Transform(
        IReadOnlyList<RailProfilePoint> points,
        ProfileRegistrationParameters parameters,
        PointCloudDeviceSide side)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        parameters.Validate("配准");
        if (points.Count == 0)
        {
            return Array.Empty<RailProfilePoint>();
        }

        IReadOnlyList<RailProfilePoint> basePoints = parameters.IsMirrored
            ? MirrorAcrossSideBoundary(points, side)
            : points;
        (double centerX, double centerY) = CalculateCentroid(basePoints);
        double radians = parameters.RotationDegrees * DegreesToRadiansFactor;
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var transformedPoints = new List<RailProfilePoint>(basePoints.Count);

        for (int index = 0; index < basePoints.Count; index++)
        {
            RailProfilePoint point = basePoints[index];
            double relativeX = point.X - centerX;
            double relativeY = point.Y - centerY;
            double transformedX = relativeX * cosValue - relativeY * sinValue + centerX + parameters.Dx;
            double transformedY = relativeX * sinValue + relativeY * cosValue + centerY + parameters.Dy;
            transformedPoints.Add(new RailProfilePoint(transformedX, transformedY));
        }

        if (parameters.XMin.HasValue || parameters.XMax.HasValue)
        {
            var filteredPoints = new List<RailProfilePoint>(transformedPoints.Count);
            for (int index = 0; index < transformedPoints.Count; index++)
            {
                RailProfilePoint point = transformedPoints[index];
                bool keep = (!parameters.XMin.HasValue || point.X >= parameters.XMin.Value) &&
                            (!parameters.XMax.HasValue || point.X <= parameters.XMax.Value);
                if (keep)
                {
                    filteredPoints.Add(point);
                }
            }

            return filteredPoints;
        }

        return transformedPoints;
    }

    private static IReadOnlyList<RailProfilePoint> MirrorAcrossSideBoundary(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side)
    {
        double axisX = side == PointCloudDeviceSide.Left
            ? ResolveMaxX(points)
            : ResolveMinX(points);
        var mirroredPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            mirroredPoints.Add(new RailProfilePoint(2.0 * axisX - point.X, point.Y));
        }

        return mirroredPoints;
    }

    private static double ResolveMinX(IReadOnlyList<RailProfilePoint> points)
    {
        double minX = points[0].X;
        for (int index = 1; index < points.Count; index++)
        {
            minX = Math.Min(minX, points[index].X);
        }

        return minX;
    }

    private static double ResolveMaxX(IReadOnlyList<RailProfilePoint> points)
    {
        double maxX = points[0].X;
        for (int index = 1; index < points.Count; index++)
        {
            maxX = Math.Max(maxX, points[index].X);
        }

        return maxX;
    }

    private static (double centerX, double centerY) CalculateCentroid(IReadOnlyList<RailProfilePoint> points)
    {
        double sumX = 0.0;
        double sumY = 0.0;
        for (int index = 0; index < points.Count; index++)
        {
            sumX += points[index].X;
            sumY += points[index].Y;
        }

        return (sumX / points.Count, sumY / points.Count);
    }

    public double CalculateAverageAbsoluteStandardError(IReadOnlyList<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            return double.NaN;
        }

        // 1. 密集采样标准曲线，用于欧氏距离匹配
        const int SampleCount = 1000;
        var standardPoints = new List<RailProfilePoint>(SampleCount + 1);
        for (int i = 0; i <= SampleCount; i++)
        {
            double x = StandardRailProfileSolver.LeftBoundaryX +
                (StandardRailProfileSolver.RightBoundaryX - StandardRailProfileSolver.LeftBoundaryX) * i / SampleCount;
            double y = StandardRailProfileSolver.RailSurfaceFun(x);
            if (!double.IsNaN(y) && !double.IsInfinity(y))
            {
                standardPoints.Add(new RailProfilePoint(x, y));
            }
        }

        if (standardPoints.Count == 0)
        {
            return double.NaN;
        }

        double sum = 0.0;
        int validCount = 0;

        // 2. 计算每个测量点到标准曲线的最短欧氏距离
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            double minDistanceSq = double.MaxValue;

            for (int j = 0; j < standardPoints.Count; j++)
            {
                RailProfilePoint sp = standardPoints[j];
                double distSq = (point.X - sp.X) * (point.X - sp.X) + (point.Y - sp.Y) * (point.Y - sp.Y);
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                }
            }

            sum += Math.Sqrt(minDistanceSq);
            validCount++;
        }

        return validCount == 0 ? double.NaN : sum / validCount;
    }
}
