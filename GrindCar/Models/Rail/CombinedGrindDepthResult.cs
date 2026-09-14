namespace GrindCar.Models.Rail;

/// <summary>
/// 单个角度的常规、掉块及最终打磨深度。
/// </summary>
public readonly record struct CombinedGrindDepthResult(
    int Angle,
    double RegularDepth,
    double DefectDepth,
    double FinalDepth);
