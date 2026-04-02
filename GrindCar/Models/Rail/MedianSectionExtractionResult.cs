using System.Collections.Generic;

namespace GrindCar.Models.Rail;

/// <summary>
/// 中位 Y 截面提取结果。
/// </summary>
public sealed class MedianSectionExtractionResult
{
    public MedianSectionExtractionResult(double medianY, IReadOnlyList<RailProfilePoint> profilePoints)
    {
        MedianY = medianY;
        ProfilePoints = profilePoints;
    }

    public double MedianY { get; }

    public IReadOnlyList<RailProfilePoint> ProfilePoints { get; }
}
