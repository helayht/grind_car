using System.Collections.Generic;

namespace GrindCar.Models.Rail;

/// <summary>
/// 代表截面提取结果。
/// </summary>
public sealed class MedianSectionExtractionResult
{
    /// <summary>
    /// 初始化代表截面提取结果。
    /// </summary>
    /// <param name="representativeY">代表 Y 值。</param>
    /// <param name="profilePoints">代表截面的二维点集。</param>
    public MedianSectionExtractionResult(double representativeY, IReadOnlyList<RailProfilePoint> profilePoints)
    {
        RepresentativeY = representativeY;
        ProfilePoints = profilePoints;
    }

    public double RepresentativeY { get; }

    public IReadOnlyList<RailProfilePoint> ProfilePoints { get; }
}
