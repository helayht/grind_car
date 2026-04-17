namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 从点云 CSV 中解析出的三维点。
/// </summary>
internal readonly record struct PointCloudPoint3D(double X, double Y, double Z);