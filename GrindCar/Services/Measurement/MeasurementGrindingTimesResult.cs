namespace GrindCar.Services.Measurement;

/// <summary>
/// 单个角度的平均打磨深度与最终打磨次数结果。
/// </summary>
/// <param name="Angle">角度值。</param>
/// <param name="AverageGrindDepth">该角度的平均打磨深度。</param>
/// <param name="GrindingTimes">向上取整后的打磨次数。</param>
public readonly record struct MeasurementGrindingTimesResult(int Angle, double AverageGrindDepth, int GrindingTimes);
