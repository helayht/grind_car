using System;
using System.Collections.Generic;
using System.Linq;
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

    internal ProfileRegistrationParameters ComposeWithGlobalDelta(
        IReadOnlyList<RailProfilePoint> basePoints,
        ProfileRegistrationParameters currentParameters,
        ProfileRegistrationParameters globalDelta,
        PointCloudDeviceSide side)
    {
        if (basePoints == null)
        {
            throw new ArgumentNullException(nameof(basePoints));
        }

        if (currentParameters == null)
        {
            throw new ArgumentNullException(nameof(currentParameters));
        }

        if (globalDelta == null)
        {
            throw new ArgumentNullException(nameof(globalDelta));
        }

        if (basePoints.Count == 0)
        {
            throw new InvalidOperationException("配准参数换算至少需要一个原始廓形点。");
        }

        currentParameters.Validate("当前配准");
        globalDelta.Validate("ICP 增量配准");

        IReadOnlyList<RailProfilePoint> orientedBasePoints = currentParameters.IsMirrored
            ? MirrorAcrossSideBoundary(basePoints, side)
            : basePoints;
        (double centerX, double centerY) = CalculateCentroid(orientedBasePoints);

        double deltaRadians = globalDelta.RotationDegrees * DegreesToRadiansFactor;
        double deltaCosValue = Math.Cos(deltaRadians);
        double deltaSinValue = Math.Sin(deltaRadians);
        double currentCentroidX = centerX + currentParameters.Dx;
        double currentCentroidY = centerY + currentParameters.Dy;
        double composedCentroidX =
            currentCentroidX * deltaCosValue - currentCentroidY * deltaSinValue + globalDelta.Dx;
        double composedCentroidY =
            currentCentroidX * deltaSinValue + currentCentroidY * deltaCosValue + globalDelta.Dy;

        return new ProfileRegistrationParameters(
            composedCentroidX - centerX,
            composedCentroidY - centerY,
            currentParameters.RotationDegrees + globalDelta.RotationDegrees,
            currentParameters.IsMirrored)
        {
            XMin = currentParameters.XMin,
            XMax = currentParameters.XMax
        };
    }

    internal ProfileRegistrationErrorMetrics CalculateStandardErrorMetrics(
        IReadOnlyList<RailProfilePoint> points,
        IReadOnlyList<RailProfilePoint> standardPoints)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (standardPoints == null)
        {
            throw new ArgumentNullException(nameof(standardPoints));
        }

        if (points.Count == 0 || standardPoints.Count == 0)
        {
            return new ProfileRegistrationErrorMetrics(
                double.NaN,
                double.NaN,
                double.NaN,
                0.0);
        }

        var distances = new List<double>(points.Count);
        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            RailProfilePoint point = points[pointIndex];
            double minimumDistanceSquared = double.MaxValue;
            for (int standardIndex = 0; standardIndex < standardPoints.Count; standardIndex++)
            {
                RailProfilePoint standardPoint = standardPoints[standardIndex];
                double deltaX = point.X - standardPoint.X;
                double deltaY = point.Y - standardPoint.Y;
                double distanceSquared = deltaX * deltaX + deltaY * deltaY;
                if (distanceSquared < minimumDistanceSquared)
                {
                    minimumDistanceSquared = distanceSquared;
                }
            }

            distances.Add(Math.Sqrt(minimumDistanceSquared));
        }

        distances.Sort();
        int inlierCount = RobustIcpRegistrationService.ResolveInlierCount(distances.Count);
        double inlierSquaredDistanceSum = 0.0;
        for (int index = 0; index < inlierCount; index++)
        {
            inlierSquaredDistanceSum += distances[index] * distances[index];
        }

        double inlierRootMeanSquareDistance = Math.Sqrt(inlierSquaredDistanceSum / inlierCount);
        double averageDistance = distances.Average();
        int percentile95Index = Math.Max(0, (int)Math.Ceiling(distances.Count * 0.95) - 1);
        double percentile95Distance = distances[percentile95Index];

        return new ProfileRegistrationErrorMetrics(
            inlierRootMeanSquareDistance,
            averageDistance,
            percentile95Distance,
            (double)inlierCount / distances.Count);
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
