using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

/// <summary>
/// 从单台设备的原始三维点云中定位最大掉块廓形。
/// </summary>
public sealed class MaximumDropProfileService
{
    private const double CoordinateTolerance = 1e-7;
    private const double DepthComparisonTolerance = 1e-9;
    private const int MinimumConsecutiveDropPointCount = 3;
    internal const double MaximumAllowedDropDepth = 3.0;

    private readonly IPointCloudProfilePreprocessor _preprocessor;
    private readonly ProfileRegistrationTransformService _registrationTransformService;

    public MaximumDropProfileService()
        : this(new ProfileRegistrationSettingsStore(), new ProfileRegistrationTransformService())
    {
    }

    internal MaximumDropProfileService(
        ProfileRegistrationSettingsStore registrationSettingsStore,
        ProfileRegistrationTransformService registrationTransformService)
    {
        if (registrationSettingsStore == null)
        {
            throw new ArgumentNullException(nameof(registrationSettingsStore));
        }

        _registrationTransformService = registrationTransformService ??
            throw new ArgumentNullException(nameof(registrationTransformService));
        _preprocessor = new PointCloudProfilePreprocessor(
            registrationSettingsStore,
            _registrationTransformService);
    }

    internal MaximumDropProfileService(
        IPointCloudProfilePreprocessor preprocessor,
        ProfileRegistrationTransformService registrationTransformService)
    {
        _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        _registrationTransformService = registrationTransformService ??
            throw new ArgumentNullException(nameof(registrationTransformService));
    }

    public MaximumDropProfileResult? AnalyzeCsv(
        string csvPath,
        PointCloudDeviceSide side,
        int sampleIndex = 0)
    {
        List<PointCloudPoint3D> points = PointCloudCsvReader.ReadPointsFromCsv(csvPath);
        return AnalyzePoints(points, side, sampleIndex);
    }

    internal MaximumDropProfileResult? AnalyzePoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side,
        int sampleIndex = 0)
    {
        PreparedPointCloudProfileData preparedData = _preprocessor.Prepare(points, side);
        return AnalyzePrepared(preparedData, sampleIndex);
    }

    internal MaximumDropProfileResult? AnalyzePrepared(
        PreparedPointCloudProfileData preparedData,
        int sampleIndex = 0)
    {
        if (preparedData == null)
        {
            throw new ArgumentNullException(nameof(preparedData));
        }

        if (!preparedData.Side.HasValue || preparedData.RegistrationParameters == null)
        {
            throw new InvalidOperationException("最大掉块分析需要包含设备侧别和配准参数的预处理点云。");
        }

        PointCloudDeviceSide side = preparedData.Side.Value;
        ProfileRegistrationParameters parameters = preparedData.RegistrationParameters;

        MaximumDropProfileResult? bestResult = null;
        bool hasAnalyzableSection = false;
        bool hasOverLimitDrop = false;
        for (int sectionIndex = 0; sectionIndex < preparedData.Sections.Count; sectionIndex++)
        {
            SectionAnalysisStatus status = TryAnalyzeSection(
                preparedData.Sections[sectionIndex],
                parameters,
                side,
                sampleIndex,
                out MaximumDropProfileResult? result);
            if (status == SectionAnalysisStatus.InsufficientData)
            {
                continue;
            }

            hasAnalyzableSection = true;
            if (status == SectionAnalysisStatus.ExceedsMaximumDepth)
            {
                hasOverLimitDrop = true;
                continue;
            }

            if (status == SectionAnalysisStatus.NoDownwardDrop)
            {
                continue;
            }

            if (bestResult == null || IsBetterCandidate(result!, bestResult))
            {
                bestResult = result;
            }
        }

        if (bestResult != null)
        {
            return bestResult;
        }

        if (hasOverLimitDrop)
        {
            throw new RepresentativeProfileExtractionException(
                $"{side} 点云中的向下掉块深度均大于 {MaximumAllowedDropDepth} mm，已判定为异常数据。");
        }

        if (hasAnalyzableSection)
        {
            return null;
        }

        throw new RepresentativeProfileExtractionException(
            $"{side} 点云未形成至少 {MinimumConsecutiveDropPointCount} 个连续有效点的掉块分析廓形。");
    }

    public static MaximumDropProfileResult SelectDeeper(
        MaximumDropProfileResult? current,
        MaximumDropProfileResult candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        return current == null || IsBetterCandidate(candidate, current)
            ? candidate
            : current;
    }

    private SectionAnalysisStatus TryAnalyzeSection(
        IReadOnlyList<PointCloudPoint3D> rawSection,
        ProfileRegistrationParameters parameters,
        PointCloudDeviceSide side,
        int sampleIndex,
        out MaximumDropProfileResult? result)
    {
        result = null;
        List<RailProfilePoint> normalizedPoints = NormalizeSection(rawSection);
        if (normalizedPoints.Count < MinimumConsecutiveDropPointCount)
        {
            return SectionAnalysisStatus.InsufficientData;
        }

        IReadOnlyList<RailProfilePoint> transformedPoints =
            _registrationTransformService.ApplyTranslationAndCrop(normalizedPoints, parameters);
        if (!TryResolveMinimumProfileHeight(transformedPoints, out double firstMinimumProfileHeight))
        {
            return SectionAnalysisStatus.InsufficientData;
        }

        List<List<RailProfilePoint>> firstFilteredSegments = SplitByMinimumProfileHeight(
            transformedPoints,
            firstMinimumProfileHeight);
        List<IReadOnlyList<RailProfilePoint>> firstValidProfileSegments =
            BuildFinalProfileSegments(firstFilteredSegments);
        if (firstValidProfileSegments.Count == 0 ||
            !TryResolveMinimumProfileHeight(
                firstValidProfileSegments,
                out double secondMinimumProfileHeight))
        {
            return SectionAnalysisStatus.InsufficientData;
        }

        List<List<RailProfilePoint>> secondFilteredSegments =
            SplitSegmentsByMinimumProfileHeight(
                firstValidProfileSegments,
                secondMinimumProfileHeight);
        List<IReadOnlyList<RailProfilePoint>> finalProfileSegments =
            BuildFinalProfileSegments(secondFilteredSegments);
        if (finalProfileSegments.Count == 0)
        {
            return SectionAnalysisStatus.InsufficientData;
        }

        bool hasValidDropRun = false;
        double maximumDropDepth = 0.0;
        RailProfilePoint maximumDropPoint = default;
        for (int segmentIndex = 0; segmentIndex < finalProfileSegments.Count; segmentIndex++)
        {
            if (!TryCalculateMaximumDownwardDepthInValidSegment(
                    finalProfileSegments[segmentIndex],
                    out double segmentMaximumDropDepth,
                    out RailProfilePoint segmentMaximumDropPoint))
            {
                continue;
            }

            if (!hasValidDropRun || segmentMaximumDropDepth > maximumDropDepth)
            {
                hasValidDropRun = true;
                maximumDropDepth = segmentMaximumDropDepth;
                maximumDropPoint = segmentMaximumDropPoint;
            }
        }

        if (!hasValidDropRun)
        {
            return SectionAnalysisStatus.NoDownwardDrop;
        }

        if (maximumDropDepth > MaximumAllowedDropDepth)
        {
            return SectionAnalysisStatus.ExceedsMaximumDepth;
        }

        double profileY = CalculateAverageY(rawSection);
        result = new MaximumDropProfileResult(
            side,
            sampleIndex,
            profileY,
            maximumDropDepth,
            finalProfileSegments,
            maximumDropPoint);
        return SectionAnalysisStatus.Valid;
    }

    private static List<IReadOnlyList<RailProfilePoint>> BuildFinalProfileSegments(
        IReadOnlyList<List<RailProfilePoint>> filteredSegments)
    {
        var finalSegments = new List<IReadOnlyList<RailProfilePoint>>();
        for (int segmentIndex = 0; segmentIndex < filteredSegments.Count; segmentIndex++)
        {
            List<RailProfilePoint> filteredSegment = filteredSegments[segmentIndex];
            if (filteredSegment.Count < MinimumConsecutiveDropPointCount)
            {
                continue;
            }

            finalSegments.Add(filteredSegment.AsReadOnly());
        }

        return finalSegments;
    }

    private static List<List<RailProfilePoint>> SplitSegmentsByMinimumProfileHeight(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments,
        double minimumProfileHeight)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        var filteredSegments = new List<List<RailProfilePoint>>();
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            List<List<RailProfilePoint>> splitSegments = SplitByMinimumProfileHeight(
                segments[segmentIndex],
                minimumProfileHeight);
            filteredSegments.AddRange(splitSegments);
        }

        return filteredSegments;
    }

    internal static bool TryCalculateMaximumDownwardDepth(
        IReadOnlyList<RailProfilePoint> points,
        double minimumProfileHeight,
        out double maximumDropDepth)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        maximumDropDepth = 0.0;
        bool hasValidDropRun = false;
        List<List<RailProfilePoint>> segments = SplitByMinimumProfileHeight(
            points,
            minimumProfileHeight);
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            if (!TryCalculateMaximumDownwardDepthInValidSegment(
                    segments[segmentIndex],
                    out double segmentMaximumDropDepth,
                    out _))
            {
                continue;
            }

            hasValidDropRun = true;
            maximumDropDepth = Math.Max(maximumDropDepth, segmentMaximumDropDepth);
        }

        return hasValidDropRun;
    }

    private static bool TryCalculateMaximumDownwardDepthInValidSegment(
        IReadOnlyList<RailProfilePoint> points,
        out double maximumDropDepth,
        out RailProfilePoint maximumDropPoint)
    {
        maximumDropDepth = 0.0;
        maximumDropPoint = default;
        int consecutiveNegativeCount = 0;
        double currentRunMinimumResidual = 0.0;
        RailProfilePoint currentRunMinimumPoint = default;
        bool hasValidDropRun = false;

        for (int index = 0; index <= points.Count; index++)
        {
            bool isNegativeResidual = false;
            double residual = 0.0;
            if (index < points.Count)
            {
                RailProfilePoint point = points[index];
                double standardZ = StandardRailProfileSolver.RailSurfaceFun(point.X);
                if (IsFinite(standardZ))
                {
                    residual = point.Y - standardZ;
                    isNegativeResidual = residual < 0.0;
                }
            }

            if (isNegativeResidual)
            {
                if (consecutiveNegativeCount == 0 || residual < currentRunMinimumResidual)
                {
                    currentRunMinimumResidual = residual;
                    currentRunMinimumPoint = points[index];
                }

                consecutiveNegativeCount++;
                continue;
            }

            if (consecutiveNegativeCount >= MinimumConsecutiveDropPointCount)
            {
                double currentRunDepth = -currentRunMinimumResidual;
                if (!hasValidDropRun || currentRunDepth > maximumDropDepth)
                {
                    hasValidDropRun = true;
                    maximumDropDepth = currentRunDepth;
                    maximumDropPoint = currentRunMinimumPoint;
                }
            }

            consecutiveNegativeCount = 0;
            currentRunMinimumResidual = 0.0;
        }

        return hasValidDropRun;
    }

    private static bool TryResolveMinimumProfileHeight(
        IReadOnlyList<RailProfilePoint> points,
        out double minimumProfileHeight)
    {
        minimumProfileHeight = 0.0;
        double minimumX = double.PositiveInfinity;
        double maximumX = double.NegativeInfinity;
        for (int index = 0; index < points.Count; index++)
        {
            double x = points[index].X;
            if (!IsFinite(x) ||
                x < StandardRailProfileSolver.LeftBoundaryX ||
                x > StandardRailProfileSolver.RightBoundaryX)
            {
                continue;
            }

            minimumX = Math.Min(minimumX, x);
            maximumX = Math.Max(maximumX, x);
        }

        if (double.IsPositiveInfinity(minimumX) || double.IsNegativeInfinity(maximumX))
        {
            return false;
        }

        minimumProfileHeight = StandardRailProfileSolver.GetMinimumProfileHeight(
            minimumX,
            maximumX);
        return true;
    }

    private static bool TryResolveMinimumProfileHeight(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments,
        out double minimumProfileHeight)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        minimumProfileHeight = 0.0;
        double minimumX = double.PositiveInfinity;
        double maximumX = double.NegativeInfinity;
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = segments[segmentIndex];
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                double x = segment[pointIndex].X;
                if (!IsFinite(x) ||
                    x < StandardRailProfileSolver.LeftBoundaryX ||
                    x > StandardRailProfileSolver.RightBoundaryX)
                {
                    continue;
                }

                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
            }
        }

        if (double.IsPositiveInfinity(minimumX) || double.IsNegativeInfinity(maximumX))
        {
            return false;
        }

        minimumProfileHeight = StandardRailProfileSolver.GetMinimumProfileHeight(
            minimumX,
            maximumX);
        return true;
    }

    private static List<RailProfilePoint> NormalizeSection(IReadOnlyList<PointCloudPoint3D> points)
    {
        var normalizedPoints = new List<RailProfilePoint>();
        double xSum = points[0].X;
        double zSum = points[0].Z;
        int valueCount = 1;
        double currentX = points[0].X;

        for (int index = 1; index < points.Count; index++)
        {
            PointCloudPoint3D point = points[index];
            if (Math.Abs(point.X - currentX) > CoordinateTolerance)
            {
                normalizedPoints.Add(new RailProfilePoint(xSum / valueCount, zSum / valueCount));
                xSum = 0.0;
                zSum = 0.0;
                valueCount = 0;
                currentX = point.X;
            }

            xSum += point.X;
            zSum += point.Z;
            valueCount++;
        }

        normalizedPoints.Add(new RailProfilePoint(xSum / valueCount, zSum / valueCount));
        return normalizedPoints;
    }

    internal static List<RailProfilePoint> FilterByMinimumProfileHeight(
        IReadOnlyList<RailProfilePoint> points,
        double minimumProfileHeight)
    {
        List<List<RailProfilePoint>> segments = SplitByMinimumProfileHeight(
            points,
            minimumProfileHeight);
        var filteredPoints = new List<RailProfilePoint>(points.Count);
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            List<RailProfilePoint> segment = segments[segmentIndex];
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                filteredPoints.Add(segment[pointIndex]);
            }
        }

        return filteredPoints;
    }

    internal static List<List<RailProfilePoint>> SplitByMinimumProfileHeight(
        IReadOnlyList<RailProfilePoint> points,
        double minimumProfileHeight)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        var segments = new List<List<RailProfilePoint>>();
        List<RailProfilePoint>? currentSegment = null;
        for (int index = 0; index < points.Count; index++)
        {
            RailProfilePoint point = points[index];
            if (point.Y < minimumProfileHeight)
            {
                currentSegment = null;
                continue;
            }

            if (currentSegment == null)
            {
                currentSegment = new List<RailProfilePoint>();
                segments.Add(currentSegment);
            }

            currentSegment.Add(point);
        }

        return segments;
    }

    private static bool IsBetterCandidate(
        MaximumDropProfileResult candidate,
        MaximumDropProfileResult current)
    {
        double depthDifference = candidate.MaximumDropDepth - current.MaximumDropDepth;
        if (Math.Abs(depthDifference) > DepthComparisonTolerance)
        {
            return depthDifference > 0.0;
        }

        if (candidate.SampleIndex != current.SampleIndex)
        {
            return candidate.SampleIndex < current.SampleIndex;
        }

        return candidate.ProfileY < current.ProfileY;
    }

    private static double CalculateAverageY(IReadOnlyList<PointCloudPoint3D> points)
    {
        double sum = 0.0;
        for (int index = 0; index < points.Count; index++)
        {
            sum += points[index].Y;
        }

        return sum / points.Count;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private enum SectionAnalysisStatus
    {
        InsufficientData,
        NoDownwardDrop,
        ExceedsMaximumDepth,
        Valid
    }
}
