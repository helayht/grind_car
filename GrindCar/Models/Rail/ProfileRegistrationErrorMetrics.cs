namespace GrindCar.Models.Rail;

/// <summary>
/// 代表廓形到标准轨廓的配准误差指标。
/// </summary>
internal sealed class ProfileRegistrationErrorMetrics
{
    public ProfileRegistrationErrorMetrics(
        double inlierRootMeanSquareDistance,
        double averageDistance,
        double percentile95Distance,
        double inlierRatio)
    {
        InlierRootMeanSquareDistance = inlierRootMeanSquareDistance;
        AverageDistance = averageDistance;
        Percentile95Distance = percentile95Distance;
        InlierRatio = inlierRatio;
    }

    public double InlierRootMeanSquareDistance { get; }

    public double AverageDistance { get; }

    public double Percentile95Distance { get; }

    public double InlierRatio { get; }
}
