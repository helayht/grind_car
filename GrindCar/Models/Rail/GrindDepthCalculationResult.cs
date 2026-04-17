using System.Collections.Generic;

namespace GrindCar.Models.Rail;

/// <summary>
/// 打磨深度计算结果，包含深度列表以及本次计算使用的代表截面点。
/// </summary>
/// <param name="Results">每个输入角度对应的打磨深度结果集合。</param>
/// <param name="RepresentativePoints">参与本次计算的代表截面点集。</param>
public sealed record GrindDepthCalculationResult(
    IReadOnlyList<GrindDepthResult> Results,
    IReadOnlyList<RailProfilePoint> RepresentativePoints);
