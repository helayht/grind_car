namespace GrindCar.Models.Rail;

/// <summary>
/// 中位 Y 截面代表点显示项。
/// 其中 X 为横坐标，Z 为纵坐标。
/// </summary>
public sealed class MedianSectionPointDisplayItem
{
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
