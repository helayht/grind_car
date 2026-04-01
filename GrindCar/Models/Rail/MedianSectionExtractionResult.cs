using System.Collections.Generic;

namespace GrindCar.Models.Rail;

/// <summary>
/// 中位 X 截面提取结果。
/// </summary>
public sealed class MedianSectionExtractionResult
{
    public MedianSectionExtractionResult(double medianX, IReadOnlyList<RailProfilePoint> profilePoints)
    {
        MedianX = medianX;
        ProfilePoints = profilePoints;
    }

    public double MedianX { get; }

    public IReadOnlyList<RailProfilePoint> ProfilePoints { get; }
}
