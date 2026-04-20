using System;
using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 代表廓形点集处理器。
/// </summary>
internal static class RepresentativeProfilePointProcessor
{
    private const int OutlierFilterWindowSize = 15;
    private const int OutlierFilterPassCount = 2;
    private const int MinNeighborCount = 6;
    private const double OutlierSigmaFactor = 2.2;
    private const double MinResidualThreshold = 0.001;
    private const double MinKeepRatio = 0.4;

    public static List<RailProfilePoint> RotateRepresentativePoints(IReadOnlyList<RailProfilePoint> points, double radians)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var rotatedPoints = new List<RailProfilePoint>(points.Count);

        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            double rotatedX = point.X * cosValue - point.Y * sinValue;
            double rotatedY = point.X * sinValue + point.Y * cosValue;
            rotatedPoints.Add(new RailProfilePoint(rotatedX, rotatedY));
        }

        return rotatedPoints;
    }

    public static List<RailProfilePoint> FilterOutlierRepresentativePoints(IReadOnlyList<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count < 5)
        {
            return new List<RailProfilePoint>(points);
        }

        List<RailProfilePoint> currentPoints = points
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        for (int passIndex = 0; passIndex < OutlierFilterPassCount; passIndex++)
        {
            if (currentPoints.Count < MinNeighborCount + 2)
            {
                break;
            }

            currentPoints = FilterOutlierRepresentativePointsSinglePass(currentPoints);
        }

        return currentPoints;
    }

    public static List<RailProfilePoint> AppendSymmetricPointsByMinX(IReadOnlyList<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            return new List<RailProfilePoint>();
        }

        double xMax = points.Max(point => point.X);
        var symmetricPoints = new List<RailProfilePoint>(points.Count * 2);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            symmetricPoints.Add(point);
            symmetricPoints.Add(new RailProfilePoint(2.0 * xMax - point.X, point.Y));
        }

        return symmetricPoints;
    }

    public static List<RailProfilePoint> TranslatePointsToBottomCenterAsOrigin(IReadOnlyList<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            return new List<RailProfilePoint>();
        }

        double xMin = points.Min(point => point.X);
        double xMax = points.Max(point => point.X);
        double yMin = points.Min(point => point.Y);

        double xCenter = (xMin + xMax) / 2.0;
        double offsetX = -xCenter;
        double offsetY = -yMin;

        var translatedPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            translatedPoints.Add(new RailProfilePoint(point.X + offsetX, point.Y + offsetY));
        }

        return translatedPoints;
    }

    public static List<RailProfilePoint> TranslatePointsToMidXReferencePointAsOrigin(IReadOnlyList<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            return new List<RailProfilePoint>();
        }

        double xMin = points.Min(point => point.X);
        double xMax = points.Max(point => point.X);
        double xMid = (xMin + xMax) / 2.0;

        RailProfilePoint referencePoint = points[0];
        double bestDistance = Math.Abs(referencePoint.X - xMid);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            double distance = Math.Abs(point.X - xMid);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                referencePoint = point;
            }
        }

        double offsetX = -referencePoint.X;
        double offsetY = -referencePoint.Y;

        var translatedPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            translatedPoints.Add(new RailProfilePoint(point.X + offsetX, point.Y + offsetY));
        }

        return translatedPoints;
    }

    private static List<RailProfilePoint> FilterOutlierRepresentativePointsSinglePass(IReadOnlyList<RailProfilePoint> sortedPoints)
    {
        int count = sortedPoints.Count;
        int halfWindow = OutlierFilterWindowSize / 2;
        var residuals = new double[count];

        for (int index = 0; index < count; index++)
        {
            int left = Math.Max(0, index - halfWindow);
            int right = Math.Min(count - 1, index + halfWindow);
            while (right - left < MinNeighborCount && (left > 0 || right < count - 1))
            {
                if (left > 0)
                {
                    left--;
                }

                if (right < count - 1)
                {
                    right++;
                }
            }

            (double slope, double intercept) = FitLineExcludingIndex(sortedPoints, left, right, index);
            RailProfilePoint point = sortedPoints[index];
            double predictedY = slope * point.X + intercept;
            residuals[index] = point.Y - predictedY;
        }

        double residualMedian = Median(residuals);
        var centeredAbsoluteResiduals = new double[count];
        for (int index = 0; index < count; index++)
        {
            centeredAbsoluteResiduals[index] = Math.Abs(residuals[index] - residualMedian);
        }

        double mad = Median(centeredAbsoluteResiduals);
        double robustSigma = 1.4826 * mad;
        double threshold = Math.Max(MinResidualThreshold, OutlierSigmaFactor * robustSigma);

        var kept = new List<RailProfilePoint>(count);
        for (int index = 0; index < count; index++)
        {
            if (centeredAbsoluteResiduals[index] <= threshold)
            {
                kept.Add(sortedPoints[index]);
            }
        }

        int minKeepCount = (int)Math.Ceiling(count * MinKeepRatio);
        if (kept.Count < minKeepCount)
        {
            int[] orderedIndexes = Enumerable.Range(0, count)
                .OrderBy(index => centeredAbsoluteResiduals[index])
                .ToArray();
            var fallback = new List<RailProfilePoint>(minKeepCount);
            for (int orderIndex = 0; orderIndex < minKeepCount; orderIndex++)
            {
                fallback.Add(sortedPoints[orderedIndexes[orderIndex]]);
            }

            fallback.Sort((left, right) => left.X.CompareTo(right.X));
            return fallback;
        }

        return kept;
    }

    private static (double slope, double intercept) FitLineExcludingIndex(
        IReadOnlyList<RailProfilePoint> points,
        int leftInclusive,
        int rightInclusive,
        int excludedIndex)
    {
        int n = 0;
        double sumX = 0.0;
        double sumY = 0.0;
        double sumXX = 0.0;
        double sumXY = 0.0;

        for (int index = leftInclusive; index <= rightInclusive; index++)
        {
            if (index == excludedIndex)
            {
                continue;
            }

            RailProfilePoint point = points[index];
            n++;
            sumX += point.X;
            sumY += point.Y;
            sumXX += point.X * point.X;
            sumXY += point.X * point.Y;
        }

        if (n <= 1)
        {
            RailProfilePoint point = points[excludedIndex];
            return (0.0, point.Y);
        }

        double denominator = n * sumXX - sumX * sumX;
        if (Math.Abs(denominator) < 1e-12)
        {
            double averageY = sumY / n;
            return (0.0, averageY);
        }

        double slope = (n * sumXY - sumX * sumY) / denominator;
        double intercept = (sumY - slope * sumX) / n;
        return (slope, intercept);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Count == 0)
        {
            throw new ArgumentException("输入数组不能为空。", nameof(values));
        }

        double[] sorted = values.OrderBy(value => value).ToArray();
        int mid = sorted.Length / 2;
        if (sorted.Length % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        return sorted[mid];
    }
}
