using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 已打磨深度检测结果。
/// </summary>
public sealed class GrindDepthDebugDetectionOutput
{
    public GrindDepthDebugDetectionOutput(DateTime baselineCreatedAt, IReadOnlyList<DetectedGrindDepthResult> results)
    {
        BaselineCreatedAt = baselineCreatedAt;
        Results = results;
    }

    public DateTime BaselineCreatedAt { get; }

    public IReadOnlyList<DetectedGrindDepthResult> Results { get; }
}