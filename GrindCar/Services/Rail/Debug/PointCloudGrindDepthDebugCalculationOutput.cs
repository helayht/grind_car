using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// Left/Right 原始点云打磨深度算法验证输出。
/// </summary>
public sealed class PointCloudGrindDepthDebugCalculationOutput
{
    public PointCloudGrindDepthDebugCalculationOutput(
        double leftRepresentativeY,
        int leftPointCount,
        double rightRepresentativeY,
        int rightPointCount,
        IReadOnlyList<GrindDepthResult> results,
        IReadOnlyList<RailProfilePoint> representativePoints)
    {
        LeftRepresentativeY = leftRepresentativeY;
        LeftPointCount = leftPointCount;
        RightRepresentativeY = rightRepresentativeY;
        RightPointCount = rightPointCount;
        Results = results;
        RepresentativePoints = representativePoints;
    }

    public double LeftRepresentativeY { get; }

    public int LeftPointCount { get; }

    public double RightRepresentativeY { get; }

    public int RightPointCount { get; }

    public IReadOnlyList<GrindDepthResult> Results { get; }

    public IReadOnlyList<RailProfilePoint> RepresentativePoints { get; }
}
