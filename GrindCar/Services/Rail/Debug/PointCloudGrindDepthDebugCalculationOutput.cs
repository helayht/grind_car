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
        IReadOnlyList<CombinedGrindDepthResult> results,
        IReadOnlyList<RailProfilePoint> representativePoints,
        MaximumDropProfileResult? leftMaximumDropProfile,
        MaximumDropProfileResult? rightMaximumDropProfile)
    {
        LeftRepresentativeY = leftRepresentativeY;
        LeftPointCount = leftPointCount;
        RightRepresentativeY = rightRepresentativeY;
        RightPointCount = rightPointCount;
        Results = results;
        RepresentativePoints = representativePoints;
        LeftMaximumDropProfile = leftMaximumDropProfile;
        RightMaximumDropProfile = rightMaximumDropProfile;
    }

    public double LeftRepresentativeY { get; }

    public int LeftPointCount { get; }

    public double RightRepresentativeY { get; }

    public int RightPointCount { get; }

    public IReadOnlyList<CombinedGrindDepthResult> Results { get; }

    public IReadOnlyList<RailProfilePoint> RepresentativePoints { get; }

    public MaximumDropProfileResult? LeftMaximumDropProfile { get; }

    public MaximumDropProfileResult? RightMaximumDropProfile { get; }

    public MaximumDropProfileResult? GlobalMaximumDropProfile
    {
        get
        {
            if (LeftMaximumDropProfile == null)
            {
                return RightMaximumDropProfile;
            }

            if (RightMaximumDropProfile == null)
            {
                return LeftMaximumDropProfile;
            }

            return LeftMaximumDropProfile.MaximumDropDepth >= RightMaximumDropProfile.MaximumDropDepth
                ? LeftMaximumDropProfile
                : RightMaximumDropProfile;
        }
    }
}
