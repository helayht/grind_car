namespace GrindCar.Services.Rail;

/// <summary>
/// 代表廓形提取参数。
/// 坐标约定：X 为前进方向，Y 为轨面横向，Z 为高度。
/// </summary>
public sealed class RepresentativeProfileExtractionOptions
{
    /// <summary>
    /// 横向 Y 分箱步长。
    /// </summary>
    public double GridStepY { get; init; } = 0.2;

    /// <summary>
    /// 轨面横向保留范围下限。
    /// </summary>
    public double RailMinY { get; init; } = -35.4;

    /// <summary>
    /// 轨面横向保留范围上限。
    /// </summary>
    public double RailMaxY { get; init; } = 35.4;

    /// <summary>
    /// 横向保留范围的额外边界。
    /// </summary>
    public double LateralMargin { get; init; } = 5.0;

    /// <summary>
    /// 单个 Y 分箱最少样本数。
    /// </summary>
    public int MinSamplesPerYBin { get; init; } = 1;
}
