namespace GrindCar.Models.Rail;

/// <summary>
/// 表示某个角度对应的代表廓形支撑直线截距。
/// </summary>
/// <param name="Angle">打磨角度。</param>
/// <param name="B">该角度对应的截距值。</param>
public readonly record struct AngleBValue(int Angle, double B);
