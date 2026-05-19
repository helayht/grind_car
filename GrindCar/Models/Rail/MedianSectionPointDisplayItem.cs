namespace GrindCar.Models.Rail;

/// <summary>
/// 代表截面点显示项。
/// 其中 X 为横坐标，Z 为纵坐标。
/// </summary>
public sealed class MedianSectionPointDisplayItem
{
    /// <summary>
    /// 初始化截面点显示项。
    /// </summary>
    /// <param name="index">点的序号。</param>
    /// <param name="x">点的横向 X 坐标。</param>
    /// <param name="z">点的高度 Z 坐标。</param>
    public MedianSectionPointDisplayItem(int index, double x, double z)
    {
        Index = index;
        X = x;
        Z = z;
    }

    public int Index { get; }

    public double X { get; }

    public double Z { get; }
}
