using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 打磨深度计算结果。
/// </summary>
public sealed class GrindDepthDebugCalculationOutput
{
    public GrindDepthDebugCalculationOutput(IReadOnlyList<GrindDepthResult> results, IReadOnlyList<RailProfilePoint> representativePoints)
    {
        Results = results;
        RepresentativePoints = representativePoints;
    }

    public IReadOnlyList<GrindDepthResult> Results { get; }

    public IReadOnlyList<RailProfilePoint> RepresentativePoints { get; }
}