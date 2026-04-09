using System.Collections.Generic;

namespace GrindCar.Models.Rail;

/// <summary>
/// 中位 Y 截面提取结果。
/// </summary>
public sealed class MedianSectionExtractionResult
{
    /// <summary>
    /// 初始化中位截面提取结果。
    /// </summary>
    /// <param name="medianY">提取到的中位 Y 值。</param>
    /// <param name="profilePoints">对应中位 Y 截面的二维点集。</param>
    public MedianSectionExtractionResult(double medianY, IReadOnlyList<RailProfilePoint> profilePoints)
    {
        MedianY = medianY;
        ProfilePoints = profilePoints;
    }

    public double MedianY { get; }

    public IReadOnlyList<RailProfilePoint> ProfilePoints { get; }
}
