using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// Left/Right 原始点云打磨深度算法验证业务服务。
/// </summary>
public class PointCloudGrindDepthDebugWorkflowService
{
    private readonly IPointCloudRepresentativeProfileService? _profileService;
    private readonly MaximumDropProfileService? _maximumDropProfileService;
    private readonly IPointCloudProfileAnalysisService? _profileAnalysisService;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> _grindDepthCalculator;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, IReadOnlyList<GrindDepthResult>> _defectGrindDepthCalculator;

    public PointCloudGrindDepthDebugWorkflowService()
        : this(
            new PointCloudProfileAnalysisService(),
            RailSurfaceService.CalculateGrindDepths,
            RailSurfaceService.CalculateDefectGrindDepths)
    {
    }

    internal PointCloudGrindDepthDebugWorkflowService(
        IPointCloudRepresentativeProfileService profileService,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> grindDepthCalculator)
        : this(profileService, null, grindDepthCalculator, RailSurfaceService.CalculateDefectGrindDepths)
    {
    }

    internal PointCloudGrindDepthDebugWorkflowService(
        IPointCloudRepresentativeProfileService profileService,
        MaximumDropProfileService? maximumDropProfileService,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> grindDepthCalculator,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, IReadOnlyList<GrindDepthResult>> defectGrindDepthCalculator)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _maximumDropProfileService = maximumDropProfileService;
        _profileAnalysisService = null;
        _grindDepthCalculator = grindDepthCalculator ?? throw new ArgumentNullException(nameof(grindDepthCalculator));
        _defectGrindDepthCalculator = defectGrindDepthCalculator ??
            throw new ArgumentNullException(nameof(defectGrindDepthCalculator));
    }

    internal PointCloudGrindDepthDebugWorkflowService(
        IPointCloudProfileAnalysisService profileAnalysisService,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> grindDepthCalculator,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, IReadOnlyList<GrindDepthResult>> defectGrindDepthCalculator)
    {
        _profileAnalysisService = profileAnalysisService ??
            throw new ArgumentNullException(nameof(profileAnalysisService));
        _profileService = null;
        _maximumDropProfileService = null;
        _grindDepthCalculator = grindDepthCalculator ?? throw new ArgumentNullException(nameof(grindDepthCalculator));
        _defectGrindDepthCalculator = defectGrindDepthCalculator ??
            throw new ArgumentNullException(nameof(defectGrindDepthCalculator));
    }

    public PointCloudGrindDepthDebugCalculationOutput Calculate(
        string leftCsvPath,
        string rightCsvPath,
        IReadOnlyList<int> angles)
    {
        if (string.IsNullOrWhiteSpace(leftCsvPath))
        {
            throw new InvalidOperationException("请选择 Left 原始点云 CSV 文件。");
        }

        if (string.IsNullOrWhiteSpace(rightCsvPath))
        {
            throw new InvalidOperationException("请选择 Right 原始点云 CSV 文件。");
        }

        if (angles == null)
        {
            throw new ArgumentNullException(nameof(angles));
        }

        MedianSectionExtractionResult leftResult;
        MedianSectionExtractionResult rightResult;
        MaximumDropProfileResult? leftMaximumDropProfile;
        MaximumDropProfileResult? rightMaximumDropProfile;
        if (_profileAnalysisService != null)
        {
            PointCloudProfileAnalysisResult leftAnalysis = AnalyzeProfile(
                leftCsvPath,
                PointCloudDeviceSide.Left);
            PointCloudProfileAnalysisResult rightAnalysis = AnalyzeProfile(
                rightCsvPath,
                PointCloudDeviceSide.Right);
            leftResult = leftAnalysis.ExtractionResult;
            rightResult = rightAnalysis.ExtractionResult;
            leftMaximumDropProfile = leftAnalysis.MaximumDropProfile;
            rightMaximumDropProfile = rightAnalysis.MaximumDropProfile;
        }
        else
        {
            leftResult = ExtractProfile(leftCsvPath, PointCloudDeviceSide.Left);
            rightResult = ExtractProfile(rightCsvPath, PointCloudDeviceSide.Right);
            leftMaximumDropProfile = AnalyzeMaximumDropProfile(
                leftCsvPath,
                PointCloudDeviceSide.Left);
            rightMaximumDropProfile = AnalyzeMaximumDropProfile(
                rightCsvPath,
                PointCloudDeviceSide.Right);
        }

        var mergedRepresentativePoints = new List<RailProfilePoint>(
            leftResult.ProfilePoints.Count + rightResult.ProfilePoints.Count);
        mergedRepresentativePoints.AddRange(leftResult.ProfilePoints);
        mergedRepresentativePoints.AddRange(rightResult.ProfilePoints);

        GrindDepthCalculationResult calculationResult =
            _grindDepthCalculator(angles, mergedRepresentativePoints);
        IReadOnlyList<CombinedGrindDepthResult> combinedResults = CombineDepths(
            angles,
            calculationResult.Results,
            leftMaximumDropProfile,
            rightMaximumDropProfile);

        return new PointCloudGrindDepthDebugCalculationOutput(
            leftResult.RepresentativeY,
            leftResult.ProfilePoints.Count,
            rightResult.RepresentativeY,
            rightResult.ProfilePoints.Count,
            combinedResults,
            calculationResult.RepresentativePoints,
            leftMaximumDropProfile,
            rightMaximumDropProfile);
    }

    private PointCloudProfileAnalysisResult AnalyzeProfile(
        string csvPath,
        PointCloudDeviceSide side)
    {
        try
        {
            return _profileAnalysisService!.AnalyzeCsv(csvPath, side);
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException(
                $"处理 {side} 原始点云及最大掉块失败：{ResolveDetailedMessage(ex)}",
                ex);
        }
    }

    private MaximumDropProfileResult? AnalyzeMaximumDropProfile(
        string csvPath,
        PointCloudDeviceSide side)
    {
        if (_maximumDropProfileService == null)
        {
            return null;
        }

        try
        {
            return _maximumDropProfileService.AnalyzeCsv(csvPath, side);
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException(
                $"处理 {side} 最大掉块廓形失败：{ResolveDetailedMessage(ex)}",
                ex);
        }
    }

    private IReadOnlyList<CombinedGrindDepthResult> CombineDepths(
        IReadOnlyList<int> angles,
        IReadOnlyList<GrindDepthResult> regularResults,
        MaximumDropProfileResult? leftMaximumDropProfile,
        MaximumDropProfileResult? rightMaximumDropProfile)
    {
        IReadOnlyDictionary<int, double> regularDepthMap = BuildDepthMap(regularResults);
        IReadOnlyDictionary<int, double> leftDepthMap = CalculateDefectDepthMap(
            angles,
            leftMaximumDropProfile);
        IReadOnlyDictionary<int, double> rightDepthMap = CalculateDefectDepthMap(
            angles,
            rightMaximumDropProfile);
        var combinedResults = new List<CombinedGrindDepthResult>(angles.Count);

        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            if (!regularDepthMap.TryGetValue(angle, out double regularDepth))
            {
                throw new InvalidOperationException($"常规打磨深度结果缺少角度 {angle}。");
            }

            double defectDepth = Math.Max(leftDepthMap[angle], rightDepthMap[angle]);
            combinedResults.Add(new CombinedGrindDepthResult(
                angle,
                regularDepth,
                defectDepth,
                Math.Max(regularDepth, defectDepth)));
        }

        return combinedResults;
    }

    private IReadOnlyDictionary<int, double> CalculateDefectDepthMap(
        IReadOnlyList<int> angles,
        MaximumDropProfileResult? maximumDropProfile)
    {
        var depthMap = new Dictionary<int, double>(angles.Count);
        var applicableAngles = new List<int>();
        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            depthMap[angle] = 0.0;
            if (maximumDropProfile != null &&
                RailSurfaceService.IsDefectAngleApplicable(maximumDropProfile.Side, angle))
            {
                applicableAngles.Add(angle);
            }
        }

        if (maximumDropProfile == null || applicableAngles.Count == 0)
        {
            return depthMap;
        }

        IReadOnlyList<GrindDepthResult> results = _defectGrindDepthCalculator(
            applicableAngles,
            maximumDropProfile.ProfilePoints);
        var calculatedAngles = new HashSet<int>();
        for (int index = 0; index < results.Count; index++)
        {
            GrindDepthResult result = results[index];
            depthMap[result.Angle] = result.GrindDepth;
            calculatedAngles.Add(result.Angle);
        }

        for (int index = 0; index < applicableAngles.Count; index++)
        {
            if (!calculatedAngles.Contains(applicableAngles[index]))
            {
                throw new InvalidOperationException(
                    $"掉块打磨深度结果缺少角度 {applicableAngles[index]}。");
            }
        }

        return depthMap;
    }

    private static IReadOnlyDictionary<int, double> BuildDepthMap(
        IReadOnlyList<GrindDepthResult> results)
    {
        var depthMap = new Dictionary<int, double>(results.Count);
        for (int index = 0; index < results.Count; index++)
        {
            depthMap[results[index].Angle] = results[index].GrindDepth;
        }

        return depthMap;
    }

    private MedianSectionExtractionResult ExtractProfile(string csvPath, PointCloudDeviceSide side)
    {
        try
        {
            return _profileService!.ExtractMedianSectionProfileFromCsv(csvPath, side);
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException(
                $"处理 {side} 原始点云失败：{ResolveDetailedMessage(ex)}",
                ex);
        }
    }

    private static string ResolveDetailedMessage(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
