using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 代表廓形点集处理器。
/// </summary>
internal static class RepresentativeProfilePointProcessor
{
    private const int BoundaryCandidateCount = 10;
    private const int MinBoundaryInlierCount = 6;
    private const double BoundaryLineSigmaFactor = 3.0;
    private const double MinBoundaryDistanceThreshold = 0.05;
    private const double BoundaryWorkSurfaceExclusionDistance = 0.5;
    private const double MaxBoundaryVerticalAngleDegrees = 60.0;
    private const double MaxRotatedBoundaryVerticalAngleDegrees = 15.0;
    private const double MinCorrectSideRatio = 0.85;
    private const double WorkSurfaceFitTolerance = -0.001;
    private const double WorkSurfaceFitLowerPercentile = 0.05;
    private const int MinDiffOutlierFilterCount = 5;
    private const double DiffOutlierSigmaFactor = 3.0;
    private const double MinDiffOutlierThreshold = 0.001;
    private const double MadScaleFactor = 1.4826;
    private const double RadiansToDegreesFactor = 180.0 / Math.PI;
    private const int MaxOutlierFilterWindowSize = 15;
    private const int MinOutlierFilterWindowSize = 5;
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

        return SelectBestRotatedAlignment(points, side).AlignedPoints;
    }

    public static List<RailProfilePoint> MirrorRepresentativePointsAcrossSideAxis(
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

        double axisX = side == PointCloudDeviceSide.Left
            ? points.Max(point => point.X)
            : points.Min(point => point.X);

        var mirroredPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            mirroredPoints.Add(new RailProfilePoint(2.0 * axisX - point.X, point.Y));
        }

        return mirroredPoints;
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
        // 根据总点数自适应窗口大小：min(MaxWindow, max(MinWindow, count/5))
        // 避免小廓形下窗口覆盖过大比例导致局部直线拟合过于平滑，丢失真实特征
        int windowSize = Math.Min(MaxOutlierFilterWindowSize,
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

    private static AlignmentCandidate SelectBestRotatedAlignment(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side)
    {
        BoundaryLineFitResult initialBoundaryLine = FitBoundaryLine(
            points,
            side,
            MaxBoundaryVerticalAngleDegrees,
            "非工作面直线倾斜超过可修正范围");
        RailProfilePoint rotationCenter = ResolveBoundaryCenter(initialBoundaryLine.Inliers);
        double rotationAngle = CalculateRotationAngleToVertical(initialBoundaryLine.Line);
        var candidates = new List<AlignmentCandidate>(2);

        double[] candidateAngles = Math.Abs(rotationAngle) < 1e-12
            ? new[] { 0.0 }
            : new[] { rotationAngle, -rotationAngle };
        var failureReasons = new List<string>(candidateAngles.Length);

        for (int index = 0; index < candidateAngles.Length; index++)
        {
            double candidateAngle = candidateAngles[index];
            if (TryCreateAlignmentCandidate(
                    points,
                    side,
                    rotationCenter,
                    candidateAngle,
                    out AlignmentCandidate candidate,
                    out string failureReason))
            {
                candidates.Add(candidate);
                continue;
            }

            failureReasons.Add(
                $"Side={side}, 候选角度={FormatDegrees(candidateAngle)}°：{failureReason}");
        }

        if (candidates.Count == 0)
        {
            string detailMessage = failureReasons.Count == 0
                ? string.Empty
                : Environment.NewLine + "候选失败详情：" + Environment.NewLine + string.Join(Environment.NewLine, failureReasons);
            throw new InvalidOperationException($"旋转对齐失败，未找到满足垂直边界和工作面贴合要求的候选结果。{detailMessage}");
        }

        return candidates
            .OrderBy(candidate => candidate.BoundaryAngleFromVerticalDegrees)
            .ThenByDescending(candidate => candidate.CorrectSideRatio)
            .First();
    }

    private static bool TryCreateAlignmentCandidate(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side,
        RailProfilePoint rotationCenter,
        double rotationAngle,
        out AlignmentCandidate candidate,
        out string failureReason)
    {
        candidate = default;
        failureReason = string.Empty;

        try
        {
            List<RailProfilePoint> rotatedPoints = RotatePointsAround(points, rotationCenter, rotationAngle);
            BoundaryLineFitResult rotatedBoundaryLine = FitBoundaryLine(
                rotatedPoints,
                side,
                MaxRotatedBoundaryVerticalAngleDegrees,
                "旋转后非工作面直线仍未接近垂直");
            double correctSideRatio = CalculateCorrectSideRatio(rotatedPoints, rotatedBoundaryLine, side);
            if (correctSideRatio < MinCorrectSideRatio)
            {
                failureReason =
                    $"工作面正确侧比例 {correctSideRatio.ToString("0.###", CultureInfo.InvariantCulture)}，低于要求 {MinCorrectSideRatio.ToString("0.###", CultureInfo.InvariantCulture)}";
                return false;
            }

            List<RailProfilePoint> alignedPoints = AlignByTranslation(rotatedPoints, rotatedBoundaryLine, side);
            if (!ValidateWorkSurfaceFit(alignedPoints, side))
            {
                failureReason = "工作面贴合标准轨廓失败";
                return false;
            }

            candidate = new AlignmentCandidate(
                alignedPoints,
                CalculateAngleFromVerticalDegrees(rotatedBoundaryLine.Line),
                correctSideRatio);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static List<RailProfilePoint> AlignByTranslation(
        IReadOnlyList<RailProfilePoint> points,
        BoundaryLineFitResult boundaryLine,
        PointCloudDeviceSide side)
    {
        double targetX = side == PointCloudDeviceSide.Left
            ? StandardRailProfileSolver.LeftBoundaryX
            : StandardRailProfileSolver.RightBoundaryX;

        double measuredBoundaryX = Median(boundaryLine.Inliers.Select(point => point.X).ToArray());
        double offsetX = targetX - measuredBoundaryX;
        List<RailProfilePoint> xAlignedPoints = TranslatePoints(points, offsetX, 0.0);
        FittedLine xAlignedBoundaryLine = boundaryLine.Line.Translate(offsetX, 0.0);
        double offsetY = CalculateStandardUpperOffsetY(xAlignedPoints, xAlignedBoundaryLine);

        var alignedPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < xAlignedPoints.Count; index++)
        {
            RailProfilePoint point = xAlignedPoints[index];
            alignedPoints.Add(new RailProfilePoint(point.X, point.Y + offsetY));
        }

        return alignedPoints;
    }

    private static BoundaryLineFitResult FitBoundaryLine(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side,
        double maxAngleFromVerticalDegrees,
        string angleErrorPrefix)
    {
        List<RailProfilePoint> candidates = SelectBoundaryCandidates(points, side);
        List<RailProfilePoint> inliers = SelectBestBoundaryInliers(candidates);
        if (inliers.Count < MinBoundaryInlierCount)
        {
            throw new InvalidOperationException("非工作面直线内点数量不足，无法完成点云对齐。");
        }

        FittedLine refinedLine = FitLineByPrincipalComponent(inliers);
        ValidateBoundaryLine(refinedLine, maxAngleFromVerticalDegrees, angleErrorPrefix);
        return new BoundaryLineFitResult(refinedLine, inliers);
    }

    private static List<RailProfilePoint> SelectBoundaryCandidates(
        IReadOnlyList<RailProfilePoint> points,
        PointCloudDeviceSide side)
    {
        if (points.Count < BoundaryCandidateCount)
        {
            throw new InvalidOperationException($"非工作面候选点数量不足，至少需要 {BoundaryCandidateCount} 个点。");
        }

        List<RailProfilePoint> sortedPoints = points
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        if (side == PointCloudDeviceSide.Left)
        {
            return sortedPoints.Take(BoundaryCandidateCount).ToList();
        }

        return sortedPoints
            .Skip(sortedPoints.Count - BoundaryCandidateCount)
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

    private static List<RailProfilePoint> SelectBestBoundaryInliers(IReadOnlyList<RailProfilePoint> candidates)
    {
        var bestInliers = new List<RailProfilePoint>();
        double bestAverageDistance = double.PositiveInfinity;

        FittedLine pcaLine = FitLineByPrincipalComponent(candidates);
        TryUpdateBestBoundaryInliers(candidates, pcaLine, ref bestInliers, ref bestAverageDistance);
        for (int leftIndex = 0; leftIndex < candidates.Count - 1; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < candidates.Count; rightIndex++)
            {
                FittedLine pairLine = FitLineFromTwoPoints(candidates[leftIndex], candidates[rightIndex]);
                TryUpdateBestBoundaryInliers(candidates, pairLine, ref bestInliers, ref bestAverageDistance);
            }
        }

        return bestInliers;
    }

    private static void TryUpdateBestBoundaryInliers(
        IReadOnlyList<RailProfilePoint> candidates,
        FittedLine line,
        ref List<RailProfilePoint> bestInliers,
        ref double bestAverageDistance)
    {
        List<RailProfilePoint> inliers = SelectBoundaryInliers(candidates, line);
        if (inliers.Count < MinBoundaryInlierCount)
        {
            return;
        }

        double averageDistance = CalculateAverageDistance(inliers, line);
        if (averageDistance < bestAverageDistance ||
            Math.Abs(averageDistance - bestAverageDistance) < 1e-12 && inliers.Count > bestInliers.Count)
        {
            bestInliers = inliers;
            bestAverageDistance = averageDistance;
        }
    }

    private static double CalculateAverageDistance(IReadOnlyList<RailProfilePoint> points, FittedLine line)
    {
        if (points.Count == 0)
        {
            return double.PositiveInfinity;
        }

        double sum = 0.0;
        for (int index = 0; index < points.Count; index++)
        {
            sum += line.DistanceTo(points[index]);
        }

        return sum / points.Count;
    }

    private static FittedLine FitLineFromTwoPoints(RailProfilePoint first, RailProfilePoint second)
    {
        double directionX = second.X - first.X;
        double directionY = second.Y - first.Y;
        double directionLength = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (directionLength <= 1e-12)
        {
            throw new InvalidOperationException("非工作面候选点退化，无法拟合直线。");
        }

        directionX /= directionLength;
        directionY /= directionLength;
        double a = -directionY;
        double b = directionX;
        double c = -(a * first.X + b * first.Y);
        return new FittedLine(a, b, c, directionX, directionY);
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

    private static void ValidateBoundaryLine(
        FittedLine line,
        double maxAngleFromVerticalDegrees,
        string errorPrefix)
    {
        double angleFromVerticalDegrees = CalculateAngleFromVerticalDegrees(line);
        if (angleFromVerticalDegrees > maxAngleFromVerticalDegrees)
        {
            throw new InvalidOperationException($"{errorPrefix}，当前夹角 {angleFromVerticalDegrees:0.###}°，允许范围 {maxAngleFromVerticalDegrees:0.###}°。");
        }
    }

    private static double CalculateAngleFromVerticalDegrees(FittedLine line)
    {
        double angleFromVerticalRadians = Math.Atan2(Math.Abs(line.DirectionX), Math.Abs(line.DirectionY));
        return angleFromVerticalRadians * RadiansToDegreesFactor;
    }

    private static string FormatDegrees(double radians)
    {
        double degrees = radians * RadiansToDegreesFactor;
        return degrees.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static double CalculateRotationAngleToVertical(FittedLine line)
    {
        double directionX = line.DirectionX;
        double directionY = line.DirectionY;
        if (directionY < 0.0)
        {
            directionX = -directionX;
            directionY = -directionY;
        }

        return Math.Atan2(directionX, directionY);
    }

    private static RailProfilePoint ResolveBoundaryCenter(IReadOnlyList<RailProfilePoint> inliers)
    {
        double medianX = Median(inliers.Select(point => point.X).ToArray());
        double medianY = Median(inliers.Select(point => point.Y).ToArray());
        return new RailProfilePoint(medianX, medianY);
    }

    private static List<RailProfilePoint> RotatePointsAround(
        IReadOnlyList<RailProfilePoint> points,
        RailProfilePoint center,
        double radians)
    {
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var rotatedPoints = new List<RailProfilePoint>(points.Count);

        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            double translatedX = point.X - center.X;
            double translatedY = point.Y - center.Y;
            double rotatedX = translatedX * cosValue - translatedY * sinValue + center.X;
            double rotatedY = translatedX * sinValue + translatedY * cosValue + center.Y;
            rotatedPoints.Add(new RailProfilePoint(rotatedX, rotatedY));
        }

        return rotatedPoints;
    }

    private static double CalculateCorrectSideRatio(
        IReadOnlyList<RailProfilePoint> points,
        BoundaryLineFitResult boundaryLine,
        PointCloudDeviceSide side)
    {
        double boundaryX = Median(boundaryLine.Inliers.Select(point => point.X).ToArray());
        int workSurfaceCount = 0;
        int correctSideCount = 0;

        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            if (boundaryLine.Line.DistanceTo(point) <= BoundaryWorkSurfaceExclusionDistance)
            {
                continue;
            }

            workSurfaceCount++;
            bool isCorrectSide = side == PointCloudDeviceSide.Left
                ? point.X > boundaryX
                : point.X < boundaryX;
            if (isCorrectSide)
            {
                correctSideCount++;
            }
        }

        if (workSurfaceCount == 0)
        {
            throw new InvalidOperationException("无法判断点云主体所在侧，未找到有效工作面点。");
        }

        return (double)correctSideCount / workSurfaceCount;
    }

    private static bool ValidateWorkSurfaceFit(IReadOnlyList<RailProfilePoint> alignedPoints, PointCloudDeviceSide side)
    {
        var diffs = new List<double>();
        for (int index = 0; index < alignedPoints.Count; index++)
        {
            RailProfilePoint point = alignedPoints[index];
            bool isWorkSurfacePoint = side == PointCloudDeviceSide.Left
                ? point.X > StandardRailProfileSolver.LeftBoundaryX + BoundaryWorkSurfaceExclusionDistance
                : point.X < StandardRailProfileSolver.RightBoundaryX - BoundaryWorkSurfaceExclusionDistance;
            if (!isWorkSurfacePoint)
            {
                continue;
            }

            double standardY = StandardRailProfileSolver.RailSurfaceFun(point.X);
            if (double.IsNaN(standardY) || double.IsInfinity(standardY))
            {
                continue;
            }

            diffs.Add(point.Y - standardY);
        }

        return diffs.Count > 0 &&
               CalculateRobustLowerDiff(diffs) >= WorkSurfaceFitTolerance;
    }

    private static double CalculateStandardUpperOffsetY(
        IReadOnlyList<RailProfilePoint> xAlignedPoints,
        FittedLine xAlignedBoundaryLine)
    {
        var diffs = new List<double>();
        for (int index = 0; index < xAlignedPoints.Count; index++)
        {
            RailProfilePoint point = xAlignedPoints[index];
            if (xAlignedBoundaryLine.DistanceTo(point) <= BoundaryWorkSurfaceExclusionDistance)
            {
                continue;
            }

            double standardY = StandardRailProfileSolver.RailSurfaceFun(point.X);
            if (double.IsNaN(standardY) || double.IsInfinity(standardY))
            {
                continue;
            }

            diffs.Add(point.Y - standardY);
        }

        if (diffs.Count == 0)
        {
            throw new InvalidOperationException("无法基于标准曲线完成 Y 方向对齐，未找到有效工作面点。");
        }

        return -CalculateRobustLowerDiff(diffs);
    }

    private static double CalculateRobustLowerDiff(IReadOnlyList<double> diffs)
    {
        IReadOnlyList<double> filteredDiffs = FilterDiffOutliersByMad(diffs);
        IReadOnlyList<double> sourceDiffs = filteredDiffs.Count > 0 ? filteredDiffs : diffs;
        return Percentile(sourceDiffs, WorkSurfaceFitLowerPercentile);
    }

    private static IReadOnlyList<double> FilterDiffOutliersByMad(IReadOnlyList<double> diffs)
    {
        if (diffs.Count < MinDiffOutlierFilterCount)
        {
            return diffs;
        }

        double median = Median(diffs);
        double[] centeredAbsoluteDiffs = new double[diffs.Count];
        for (int index = 0; index < diffs.Count; index++)
        {
            centeredAbsoluteDiffs[index] = Math.Abs(diffs[index] - median);
        }

        double mad = Median(centeredAbsoluteDiffs);
        double robustSigma = MadScaleFactor * mad;
        double threshold = Math.Max(MinDiffOutlierThreshold, DiffOutlierSigmaFactor * robustSigma);
        var filteredDiffs = new List<double>(diffs.Count);
        for (int index = 0; index < diffs.Count; index++)
        {
            if (Math.Abs(diffs[index] - median) <= threshold)
            {
                filteredDiffs.Add(diffs[index]);
            }
        }

        return filteredDiffs;
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("输入数组不能为空。", nameof(values));
        }

        double clampedPercentile = Math.Min(1.0, Math.Max(0.0, percentile));
        double[] sortedValues = values.ToArray();
        Array.Sort(sortedValues);

        double position = (sortedValues.Length - 1) * clampedPercentile;
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        double weight = position - lowerIndex;
        return sortedValues[lowerIndex] * (1.0 - weight) + sortedValues[upperIndex] * weight;
    }

    private static List<RailProfilePoint> TranslatePoints(
        IReadOnlyList<RailProfilePoint> points,
        double offsetX,
        double offsetY)
    {
        var translatedPoints = new List<RailProfilePoint>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            translatedPoints.Add(new RailProfilePoint(point.X + offsetX, point.Y + offsetY));
        }

        return translatedPoints;
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

        double[] firstSelectionValues = values.ToArray();
        int mid = firstSelectionValues.Length / 2;
        if (firstSelectionValues.Length % 2 == 0)
        {
            double[] secondSelectionValues = values.ToArray();
            double lowerMedian = QuickSelect.SelectKthSmallest(firstSelectionValues, mid - 1);
            double upperMedian = QuickSelect.SelectKthSmallest(secondSelectionValues, mid);
            return (lowerMedian + upperMedian) / 2.0;
        }

        return QuickSelect.SelectKthSmallest(firstSelectionValues, mid);
    }

    private readonly record struct BoundaryLineFitResult(FittedLine Line, IReadOnlyList<RailProfilePoint> Inliers);

    private readonly record struct AlignmentCandidate(
        List<RailProfilePoint> AlignedPoints,
        double BoundaryAngleFromVerticalDegrees,
        double CorrectSideRatio);

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

        public FittedLine Translate(double offsetX, double offsetY)
        {
            double translatedC = C - A * offsetX - B * offsetY;
            return new FittedLine(A, B, translatedC, DirectionX, DirectionY);
        }
    }
}
