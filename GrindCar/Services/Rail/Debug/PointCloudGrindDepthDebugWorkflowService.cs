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
    private readonly IPointCloudRepresentativeProfileService _profileService;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> _grindDepthCalculator;

    public PointCloudGrindDepthDebugWorkflowService()
        : this(new PointCloudRepresentativeProfileService(), RailSurfaceService.CalculateGrindDepths)
    {
    }

    internal PointCloudGrindDepthDebugWorkflowService(
        IPointCloudRepresentativeProfileService profileService,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> grindDepthCalculator)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _grindDepthCalculator = grindDepthCalculator ?? throw new ArgumentNullException(nameof(grindDepthCalculator));
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

        MedianSectionExtractionResult leftResult = ExtractProfile(leftCsvPath, PointCloudDeviceSide.Left);
        MedianSectionExtractionResult rightResult = ExtractProfile(rightCsvPath, PointCloudDeviceSide.Right);

        var mergedRepresentativePoints = new List<RailProfilePoint>(
            leftResult.ProfilePoints.Count + rightResult.ProfilePoints.Count);
        mergedRepresentativePoints.AddRange(leftResult.ProfilePoints);
        mergedRepresentativePoints.AddRange(rightResult.ProfilePoints);

        GrindDepthCalculationResult calculationResult =
            _grindDepthCalculator(angles, mergedRepresentativePoints);

        return new PointCloudGrindDepthDebugCalculationOutput(
            leftResult.RepresentativeY,
            leftResult.ProfilePoints.Count,
            rightResult.RepresentativeY,
            rightResult.ProfilePoints.Count,
            calculationResult.Results,
            calculationResult.RepresentativePoints);
    }

    private MedianSectionExtractionResult ExtractProfile(string csvPath, PointCloudDeviceSide side)
    {
        try
        {
            return _profileService.ExtractMedianSectionProfileFromCsv(csvPath, side);
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
