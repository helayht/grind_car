using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 打磨深度调试业务服务。
/// </summary>
public class GrindDepthDebugWorkflowService
{
    public GrindDepthDebugCalculationOutput CalculateAndSaveBaseline(IReadOnlyList<int> angles)
    {
        GrindDepthCalculationResult calculationResult = RailSurfaceService.CalculateGrindDepths(angles);
        GrindingDepthBaseline baseline =
            RailSurfaceService.CreateGrindingDepthBaseline(angles, calculationResult.RepresentativePoints);
        RailSurfaceService.SaveGrindingDepthBaseline(baseline);

        return new GrindDepthDebugCalculationOutput(calculationResult.Results, calculationResult.RepresentativePoints);
    }

    public GrindDepthDebugDetectionOutput DetectFromLatestBaseline()
    {
        GrindingDepthBaseline? baseline = RailSurfaceService.LoadLatestGrindingDepthBaseline();
        if (baseline == null)
        {
            throw new InvalidOperationException("未找到检测基线，请先执行“计算需要打磨深度”。");
        }

        IReadOnlyList<DetectedGrindDepthResult> results = RailSurfaceService.DetectGrindingDepths(baseline);
        return new GrindDepthDebugDetectionOutput(baseline.CreatedAt, results);
    }
}