namespace GrindCar.Models.Rail;

/// <summary>
/// 表示已打磨深度检测结果。
/// </summary>
/// <param name="Angle">打磨角度。</param>
/// <param name="BaselineB">基线 b 值。</param>
/// <param name="CurrentB">当前测得 b 值。</param>
/// <param name="DetectedDepth">已打磨深度。</param>
public readonly record struct DetectedGrindDepthResult(
    int Angle,
    double BaselineB,
    double CurrentB,
    double DetectedDepth);
