namespace GrindCar.Models.Rail;

/// <summary>
/// 表示轨面截面的二维点。
/// </summary>
/// <param name="X">轨面横向坐标。</param>
/// <param name="Y">轨面高度坐标。</param>
public readonly record struct RailProfilePoint(double X, double Y);
