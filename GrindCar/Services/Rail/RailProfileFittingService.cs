using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

public sealed class RailProfileFittingService : IRailProfileFittingService
{
    private const int MinRequiredPointCount = 4;
    private const double DuplicateMergeTolerance = 1e-6;
    private const int SmoothingWindowRadius = 3;
    private const double OutlierMadScale = 6.0;
    private const double SingularTolerance = 1e-12;

    public RailProfileFitResult Fit(IReadOnlyCollection<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        IReadOnlyList<RailProfilePoint> orderedPoints = NormalizePoints(points);
        if (orderedPoints.Count < MinRequiredPointCount)
        {
            throw new InvalidOperationException($"拟合至少需要 {MinRequiredPointCount.ToString(CultureInfo.InvariantCulture)} 个有效点。");
        }

        IReadOnlyList<RailProfilePoint> filteredPoints = RemoveOutliers(orderedPoints);
        if (filteredPoints.Count < MinRequiredPointCount)
        {
            throw new InvalidOperationException($"去除异常点后剩余点数不足 {MinRequiredPointCount.ToString(CultureInfo.InvariantCulture)} 个。");
        }

        double[] x = filteredPoints.Select(point => point.X).ToArray();
        double[] smoothedY = SmoothPoints(filteredPoints);
        var spline = NaturalCubicSpline.Create(x, smoothedY);

        return new RailProfileFitResult(spline.Evaluate, x[0], x[^1]);
    }

    private static IReadOnlyList<RailProfilePoint> NormalizePoints(IReadOnlyCollection<RailProfilePoint> points)
    {
        List<RailProfilePoint> ordered = points
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<RailProfilePoint>();
        }

        var normalized = new List<RailProfilePoint>(ordered.Count);
        double currentX = ordered[0].X;
        double ySum = ordered[0].Y;
        int count = 1;

        for (int index = 1; index < ordered.Count; index++)
        {
            RailProfilePoint point = ordered[index];
            if (Math.Abs(point.X - currentX) <= DuplicateMergeTolerance)
            {
                ySum += point.Y;
                count++;
                continue;
            }

            normalized.Add(new RailProfilePoint(currentX, ySum / count));
            currentX = point.X;
            ySum = point.Y;
            count = 1;
        }

        normalized.Add(new RailProfilePoint(currentX, ySum / count));
        return normalized;
    }

    private static IReadOnlyList<RailProfilePoint> RemoveOutliers(IReadOnlyList<RailProfilePoint> points)
    {
        if (points.Count <= MinRequiredPointCount)
        {
            return points;
        }

        double[] residuals = new double[points.Count];
        residuals[0] = 0.0;
        residuals[^1] = 0.0;

        for (int index = 1; index < points.Count - 1; index++)
        {
            RailProfilePoint previous = points[index - 1];
            RailProfilePoint current = points[index];
            RailProfilePoint next = points[index + 1];
            double ratio = (current.X - previous.X) / (next.X - previous.X);
            double expectedY = previous.Y + (next.Y - previous.Y) * ratio;
            residuals[index] = Math.Abs(current.Y - expectedY);
        }

        double threshold = ComputeResidualThreshold(residuals);
        if (threshold <= 0.0)
        {
            return points;
        }

        var filtered = new List<RailProfilePoint>(points.Count);
        filtered.Add(points[0]);

        for (int index = 1; index < points.Count - 1; index++)
        {
            if (residuals[index] <= threshold)
            {
                filtered.Add(points[index]);
            }
        }

        filtered.Add(points[^1]);
        return filtered.Count >= MinRequiredPointCount ? filtered : points;
    }

    private static double ComputeResidualThreshold(IReadOnlyList<double> residuals)
    {
        double[] sorted = residuals
            .Where(value => value > 0.0)
            .OrderBy(value => value)
            .ToArray();

        if (sorted.Length == 0)
        {
            return 0.0;
        }

        double median = ComputeMedian(sorted);
        double[] deviations = sorted
            .Select(value => Math.Abs(value - median))
            .OrderBy(value => value)
            .ToArray();

        double mad = ComputeMedian(deviations);
        if (mad <= 0.0)
        {
            return median * 3.0;
        }

        return median + OutlierMadScale * mad;
    }

    private static double ComputeMedian(IReadOnlyList<double> values)
    {
        int middle = values.Count / 2;
        if (values.Count % 2 == 0)
        {
            return (values[middle - 1] + values[middle]) / 2.0;
        }

        return values[middle];
    }

    private static double[] SmoothPoints(IReadOnlyList<RailProfilePoint> points)
    {
        var smoothed = new double[points.Count];

        for (int index = 0; index < points.Count; index++)
        {
            smoothed[index] = SmoothPoint(points, index);
        }

        return smoothed;
    }

    private static double SmoothPoint(IReadOnlyList<RailProfilePoint> points, int centerIndex)
    {
        int start = Math.Max(0, centerIndex - SmoothingWindowRadius);
        int end = Math.Min(points.Count - 1, centerIndex + SmoothingWindowRadius);

        if (end - start + 1 < 3)
        {
            return points[centerIndex].Y;
        }

        double centerX = points[centerIndex].X;
        double maxDistance = Math.Max(
            Math.Abs(points[end].X - centerX),
            Math.Abs(centerX - points[start].X));

        if (maxDistance <= DuplicateMergeTolerance)
        {
            return points[centerIndex].Y;
        }

        double s0 = 0.0;
        double s1 = 0.0;
        double s2 = 0.0;
        double s3 = 0.0;
        double s4 = 0.0;
        double t0 = 0.0;
        double t1 = 0.0;
        double t2 = 0.0;

        for (int index = start; index <= end; index++)
        {
            RailProfilePoint point = points[index];
            double dx = point.X - centerX;
            double normalizedDistance = Math.Abs(dx) / maxDistance;
            double weight = Tricube(normalizedDistance);
            double dx2 = dx * dx;

            s0 += weight;
            s1 += weight * dx;
            s2 += weight * dx2;
            s3 += weight * dx2 * dx;
            s4 += weight * dx2 * dx2;

            t0 += weight * point.Y;
            t1 += weight * dx * point.Y;
            t2 += weight * dx2 * point.Y;
        }

        double[,] matrix =
        {
            { s0, s1, s2 },
            { s1, s2, s3 },
            { s2, s3, s4 }
        };
        double[] vector = { t0, t1, t2 };

        if (TrySolveLinear3x3(matrix, vector, out double[] coefficients))
        {
            return coefficients[0];
        }

        double weightedAverage = 0.0;
        double totalWeight = 0.0;
        for (int index = start; index <= end; index++)
        {
            RailProfilePoint point = points[index];
            double normalizedDistance = Math.Abs(point.X - centerX) / maxDistance;
            double weight = Tricube(normalizedDistance);
            weightedAverage += point.Y * weight;
            totalWeight += weight;
        }

        return totalWeight > 0.0 ? weightedAverage / totalWeight : points[centerIndex].Y;
    }

    private static double Tricube(double value)
    {
        if (value >= 1.0)
        {
            return 0.0;
        }

        double term = 1.0 - value * value * value;
        return term * term * term;
    }

    private static bool TrySolveLinear3x3(double[,] matrix, double[] vector, out double[] solution)
    {
        double[,] a =
        {
            { matrix[0, 0], matrix[0, 1], matrix[0, 2], vector[0] },
            { matrix[1, 0], matrix[1, 1], matrix[1, 2], vector[1] },
            { matrix[2, 0], matrix[2, 1], matrix[2, 2], vector[2] }
        };

        for (int pivot = 0; pivot < 3; pivot++)
        {
            int maxRow = pivot;
            double maxValue = Math.Abs(a[pivot, pivot]);
            for (int row = pivot + 1; row < 3; row++)
            {
                double candidate = Math.Abs(a[row, pivot]);
                if (candidate > maxValue)
                {
                    maxValue = candidate;
                    maxRow = row;
                }
            }

            if (maxValue < SingularTolerance)
            {
                solution = Array.Empty<double>();
                return false;
            }

            if (maxRow != pivot)
            {
                SwapRows(a, pivot, maxRow);
            }

            double pivotValue = a[pivot, pivot];
            for (int column = pivot; column < 4; column++)
            {
                a[pivot, column] /= pivotValue;
            }

            for (int row = 0; row < 3; row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                double factor = a[row, pivot];
                for (int column = pivot; column < 4; column++)
                {
                    a[row, column] -= factor * a[pivot, column];
                }
            }
        }

        solution = new[] { a[0, 3], a[1, 3], a[2, 3] };
        return true;
    }

    private static void SwapRows(double[,] matrix, int rowA, int rowB)
    {
        for (int column = 0; column < matrix.GetLength(1); column++)
        {
            (matrix[rowA, column], matrix[rowB, column]) = (matrix[rowB, column], matrix[rowA, column]);
        }
    }

    private sealed class NaturalCubicSpline
    {
        private readonly double[] _x;
        private readonly double[] _a;
        private readonly double[] _b;
        private readonly double[] _c;
        private readonly double[] _d;

        private NaturalCubicSpline(double[] x, double[] a, double[] b, double[] c, double[] d)
        {
            _x = x;
            _a = a;
            _b = b;
            _c = c;
            _d = d;
        }

        public static NaturalCubicSpline Create(double[] x, double[] y)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            if (x.Length != y.Length)
            {
                throw new ArgumentException("x 与 y 的点数不一致。");
            }

            if (x.Length < 2)
            {
                throw new ArgumentException("至少需要两个点构造样条。");
            }

            int count = x.Length;
            double[] h = new double[count - 1];
            for (int index = 0; index < count - 1; index++)
            {
                h[index] = x[index + 1] - x[index];
                if (h[index] <= 0.0)
                {
                    throw new ArgumentException("x 必须严格递增。");
                }
            }

            double[] alpha = new double[count];
            for (int index = 1; index < count - 1; index++)
            {
                alpha[index] = 3.0 * ((y[index + 1] - y[index]) / h[index] - (y[index] - y[index - 1]) / h[index - 1]);
            }

            double[] l = new double[count];
            double[] mu = new double[count];
            double[] z = new double[count];
            l[0] = 1.0;

            for (int index = 1; index < count - 1; index++)
            {
                l[index] = 2.0 * (x[index + 1] - x[index - 1]) - h[index - 1] * mu[index - 1];
                mu[index] = h[index] / l[index];
                z[index] = (alpha[index] - h[index - 1] * z[index - 1]) / l[index];
            }

            l[count - 1] = 1.0;
            double[] a = y.ToArray();
            double[] b = new double[count - 1];
            double[] c = new double[count];
            double[] d = new double[count - 1];

            for (int index = count - 2; index >= 0; index--)
            {
                c[index] = z[index] - mu[index] * c[index + 1];
                b[index] = (a[index + 1] - a[index]) / h[index] - h[index] * (c[index + 1] + 2.0 * c[index]) / 3.0;
                d[index] = (c[index + 1] - c[index]) / (3.0 * h[index]);
            }

            return new NaturalCubicSpline(x, a, b, c, d);
        }

        public double Evaluate(double x)
        {
            int segmentIndex = FindSegment(x);
            double dx = x - _x[segmentIndex];
            return _a[segmentIndex]
                   + _b[segmentIndex] * dx
                   + _c[segmentIndex] * dx * dx
                   + _d[segmentIndex] * dx * dx * dx;
        }

        private int FindSegment(double x)
        {
            int index = Array.BinarySearch(_x, x);
            if (index >= 0)
            {
                return Math.Min(index, _x.Length - 2);
            }

            int insertionIndex = ~index;
            if (insertionIndex <= 0)
            {
                return 0;
            }

            if (insertionIndex >= _x.Length)
            {
                return _x.Length - 2;
            }

            return insertionIndex - 1;
        }
    }
}
