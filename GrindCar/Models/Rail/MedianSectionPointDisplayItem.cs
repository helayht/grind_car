namespace GrindCar.Models.Rail;

/// <summary>
/// 中位 X 截面代表点显示项。
/// 其中 Y 为横坐标，Z 为纵坐标。
/// </summary>
public sealed class MedianSectionPointDisplayItem
{
    public MedianSectionPointDisplayItem(int index, double y, double z)
    {
        Index = index;
        Y = y;
        Z = z;
    }

    public int Index { get; }

    public double Y { get; }

    public double Z { get; }
}
