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
    private const int MaxOutlierFilterWindowSize = 15;
    private const int MinOutlierFilterWindowSize = 5;
    private const int OutlierFilterPassCount = 2;
    private const int MinNeighborCount = 6;
    private const double OutlierSigmaFactor = 2.2;
    private const double MinResidualThreshold = 0.001;
    private const double MinKeepRatio = 0.4;

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

    private static List<RailProfilePoint> FilterOutlierRepresentativePointsSinglePass(
        IReadOnlyList<RailProfilePoint> sortedPoints)
    {
        int count = sortedPoints.Count;
        // 根据总点数自适应窗口大小：min(MaxWindow, max(MinWindow, count/5))
        // 避免小廓形下窗口覆盖过大比例导致局部直线拟合过于平滑，丢失真实特征
        int windowSize = Math.Min(
            MaxOutlierFilterWindowSize,
            Math.Max(MinOutlierFilterWindowSize, count / 5));
        int halfWindow = windowSize / 2;
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

            FittedLine localLine = FitLineByPrincipalComponentExcludingIndex(
                sortedPoints,
                left,
                right,
                index);
            RailProfilePoint point = sortedPoints[index];
            residuals[index] = localLine.DistanceTo(point);
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

    private static FittedLine FitLineByPrincipalComponentExcludingIndex(
        IReadOnlyList<RailProfilePoint> points,
        int leftInclusive,
        int rightInclusive,
        int excludedIndex)
    {
        int pointCount = 0;
        double sumX = 0.0;
        double sumY = 0.0;
        for (int index = leftInclusive; index <= rightInclusive; index++)
        {
            if (index == excludedIndex)
            {
                continue;
            }

            pointCount++;
            sumX += points[index].X;
            sumY += points[index].Y;
        }

        if (pointCount < 2)
        {
            return new FittedLine(1.0, 0.0, -points[excludedIndex].X);
        }

        double meanX = sumX / pointCount;
        double meanY = sumY / pointCount;
        double covarianceXX = 0.0;
        double covarianceXY = 0.0;
        double covarianceYY = 0.0;
        for (int index = leftInclusive; index <= rightInclusive; index++)
        {
            if (index == excludedIndex)
            {
                continue;
            }

            double deltaX = points[index].X - meanX;
            double deltaY = points[index].Y - meanY;
            covarianceXX += deltaX * deltaX;
            covarianceXY += deltaX * deltaY;
            covarianceYY += deltaY * deltaY;
        }

        double trace = covarianceXX + covarianceYY;
        double determinant = covarianceXX * covarianceYY - covarianceXY * covarianceXY;
        double discriminant = Math.Max(0.0, trace * trace / 4.0 - determinant);
        double principalEigenvalue = trace / 2.0 + Math.Sqrt(discriminant);

        double directionX = covarianceXY;
        double directionY = principalEigenvalue - covarianceXX;
        if (Math.Abs(covarianceXY) <= 1e-12 && Math.Abs(principalEigenvalue - covarianceXX) <= 1e-12)
        {
            directionX = 1.0;
            directionY = 0.0;
        }

        double length = Math.Sqrt(directionX * directionX + directionY * directionY);
        double coefficientA = -(directionY / length);
        double coefficientB = directionX / length;
        double coefficientC = -(coefficientA * meanX + coefficientB * meanY);

        return new FittedLine(coefficientA, coefficientB, coefficientC);
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

        double[] firstSelectionValues = values.ToArray();
        int middleIndex = firstSelectionValues.Length / 2;
        if (firstSelectionValues.Length % 2 == 0)
        {
            double[] secondSelectionValues = values.ToArray();
            double lowerMedian = QuickSelect.SelectKthSmallest(firstSelectionValues, middleIndex - 1);
            double upperMedian = QuickSelect.SelectKthSmallest(secondSelectionValues, middleIndex);
            return (lowerMedian + upperMedian) / 2.0;
        }

        return QuickSelect.SelectKthSmallest(firstSelectionValues, middleIndex);
    }

    private readonly record struct FittedLine(double A, double B, double C)
    {
        public double DistanceTo(RailProfilePoint point)
        {
            return Math.Abs(A * point.X + B * point.Y + C);
        }
    }
}
