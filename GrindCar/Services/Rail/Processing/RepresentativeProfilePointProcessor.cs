using System;
using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 代表廓形点集处理器。
/// </summary>
internal static class RepresentativeProfilePointProcessor
{
    private const double BoundaryCandidateRatio = 0.2;
    private const double TopInlierRatio = 0.1;
    private const int MinBoundaryCandidateCount = 8;
    private const int MinBoundaryInlierCount = 6;
    private const double BoundaryLineSigmaFactor = 3.0;
    private const double MinBoundaryDistanceThreshold = 0.05;
    private const double MaxBoundaryVerticalAngleDegrees = 20.0;
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

    public static List<RailProfilePoint> AlignRepresentativePointsToStandardBoundary(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            return new List<RailProfilePoint>();
        }

        // 每台廓形仪只采集半边轨面，先用外侧非工作面直线确定该半边的标准边界。
        BoundaryLineFitResult boundaryLine = FitBoundaryLine(points, side);
        RailProfilePoint measuredCorner = ResolveTopBoundaryPoint(boundaryLine.Inliers);
        double targetX = side == PointCloudDeviceSide.Left
            ? StandardRailProfileSolver.LeftBoundaryX
            : StandardRailProfileSolver.RightBoundaryX;
        double targetY = StandardRailProfileSolver.RailSurfaceFun(targetX);
        if (double.IsNaN(targetY) || double.IsInfinity(targetY))
        {
            throw new InvalidOperationException($"标准轨面边界点无效: X={targetX}");
        }

        // 本阶段只做平移，不做自动旋转，避免把安装角度误差和真实轨面磨耗混在一起。
        double offsetX = targetX - measuredCorner.X;
        double offsetY = targetY - measuredCorner.Y;

        var alignedPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            alignedPoints.Add(new RailProfilePoint(point.X + offsetX, point.Y + offsetY));
        }

        return alignedPoints;
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

    private static BoundaryLineFitResult FitBoundaryLine(IReadOnlyList<RailProfilePoint> points, PointCloudDeviceSide side)
    {
        List<RailProfilePoint> candidates = SelectBoundaryCandidates(points, side);
        FittedLine initialLine = FitLineByPrincipalComponent(candidates);
        // 按点到通用直线 ax+by+c=0 的垂直距离剔除离群点，再重拟合最终边界线。
        List<RailProfilePoint> inliers = SelectBoundaryInliers(candidates, initialLine);
        if (inliers.Count < MinBoundaryInlierCount)
        {
            throw new InvalidOperationException("非工作面直线内点数量不足，无法完成点云对齐。");
        }

        FittedLine refinedLine = FitLineByPrincipalComponent(inliers);
        ValidateBoundaryLine(refinedLine);
        return new BoundaryLineFitResult(refinedLine, inliers);
    }

    private static List<RailProfilePoint> SelectBoundaryCandidates(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side)
    {
        // 侧别来自配置文件，因此候选区只取对应外侧的固定比例窗口，不再自动猜左右。
        int candidateCount = Math.Max(MinBoundaryCandidateCount, (int)Math.Ceiling(points.Count * BoundaryCandidateRatio));
        candidateCount = Math.Min(candidateCount, points.Count);

        List<RailProfilePoint> sortedPoints = points
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        if (side == PointCloudDeviceSide.Left)
        {
            return sortedPoints.Take(candidateCount).ToList();
        }

        return sortedPoints
            .Skip(sortedPoints.Count - candidateCount)
            .ToList();
    }

    private static List<RailProfilePoint> SelectBoundaryInliers(
        IReadOnlyList<RailProfilePoint> candidates,
        FittedLine line)
    {
        var distances = new double[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
        {
            distances[index] = line.DistanceTo(candidates[index]);
        }

        double medianDistance = Median(distances);
        var centeredDistances = new double[distances.Length];
        for (int index = 0; index < distances.Length; index++)
        {
            centeredDistances[index] = Math.Abs(distances[index] - medianDistance);
        }

        double mad = Median(centeredDistances);
        double robustSigma = 1.4826 * mad;
        double threshold = Math.Max(MinBoundaryDistanceThreshold, medianDistance + BoundaryLineSigmaFactor * robustSigma);

        var inliers = new List<RailProfilePoint>(candidates.Count);
        for (int index = 0; index < candidates.Count; index++)
        {
            if (distances[index] <= threshold)
            {
                inliers.Add(candidates[index]);
            }
        }

        return inliers;
    }

    private static FittedLine FitLineByPrincipalComponent(IReadOnlyList<RailProfilePoint> points)
    {
        if (points.Count < 2)
        {
            throw new InvalidOperationException("非工作面候选点数量不足，无法拟合直线。");
        }

        double meanX = points.Average(point => point.X);
        double meanY = points.Average(point => point.Y);
        double covarianceXX = 0.0;
        double covarianceXY = 0.0;
        double covarianceYY = 0.0;

        for (int index = 0; index < points.Count; index++)
        {
            double dx = points[index].X - meanX;
            double dy = points[index].Y - meanY;
            covarianceXX += dx * dx;
            covarianceXY += dx * dy;
            covarianceYY += dy * dy;
        }

        double trace = covarianceXX + covarianceYY;
        double determinant = covarianceXX * covarianceYY - covarianceXY * covarianceXY;
        double discriminant = Math.Max(0.0, trace * trace / 4.0 - determinant);
        double principalEigenvalue = trace / 2.0 + Math.Sqrt(discriminant);

        double directionX;
        double directionY;
        if (Math.Abs(covarianceXY) > 1e-12 || Math.Abs(principalEigenvalue - covarianceXX) > 1e-12)
        {
            directionX = covarianceXY;
            directionY = principalEigenvalue - covarianceXX;
        }
        else
        {
            directionX = 1.0;
            directionY = 0.0;
        }

        double directionLength = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (directionLength <= 1e-12)
        {
            throw new InvalidOperationException("非工作面候选点退化，无法拟合直线。");
        }

        directionX /= directionLength;
        directionY /= directionLength;

        // PCA/TLS 对近似竖直线更稳定，不需要把竖直边强行写成 y=ax+b。
        double a = -directionY;
        double b = directionX;
        double c = -(a * meanX + b * meanY);
        return new FittedLine(a, b, c, directionX, directionY);
    }

    private static void ValidateBoundaryLine(FittedLine line)
    {
        double angleFromVerticalRadians = Math.Atan2(Math.Abs(line.DirectionX), Math.Abs(line.DirectionY));
        double angleFromVerticalDegrees = angleFromVerticalRadians * 180.0 / Math.PI;
        if (angleFromVerticalDegrees > MaxBoundaryVerticalAngleDegrees)
        {
            throw new InvalidOperationException($"非工作面直线不接近竖直方向，当前夹角 {angleFromVerticalDegrees:0.###}°。");
        }
    }

    private static RailProfilePoint ResolveTopBoundaryPoint(IReadOnlyList<RailProfilePoint> inliers)
    {
        if (inliers.Count == 0)
        {
            throw new InvalidOperationException("非工作面直线内点为空，无法确定临界点。");
        }

        // 顶部若干内点取中位值，降低单个边缘噪点对上下平移的影响。
        int topCount = Math.Max(1, (int)Math.Ceiling(inliers.Count * TopInlierRatio));
        RailProfilePoint[] topPoints = inliers
            .OrderByDescending(point => point.Y)
            .Take(topCount)
            .ToArray();

        double[] xValues = topPoints.Select(point => point.X).ToArray();
        double[] yValues = topPoints.Select(point => point.Y).ToArray();
        return new RailProfilePoint(Median(xValues), Median(yValues));
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

    private readonly record struct BoundaryLineFitResult(FittedLine Line, IReadOnlyList<RailProfilePoint> Inliers);

    private readonly record struct FittedLine(
        double A,
        double B,
        double C,
        double DirectionX,
        double DirectionY)
    {
        public double DistanceTo(RailProfilePoint point)
        {
            return Math.Abs(A * point.X + B * point.Y + C);
        }
    }
}
