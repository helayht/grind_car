namespace GrindCar.Models.Rail;

/// <summary>
/// 打磨深度计算结果，保留输入角度与对应深度的配对关系。
/// </summary>
/// <param name="Angle">输入的打磨角度。</param>
/// <param name="GrindDepth">该角度对应的打磨深度。</param>
public readonly record struct GrindDepthResult(int Angle, double GrindDepth);
