namespace GrindCar.Services.Measurement;

/// <summary>
/// 单个角度的常规、掉块、最终打磨深度与打磨次数。
/// </summary>
public readonly record struct MeasurementGrindingTimesResult
{
    public MeasurementGrindingTimesResult(int angle, double averageGrindDepth, int grindingTimes)
        : this(angle, averageGrindDepth, 0.0, averageGrindDepth, grindingTimes)
    {
    }

    public MeasurementGrindingTimesResult(
        int angle,
        double regularAverageDepth,
        double defectDepth,
        double finalGrindDepth,
        int grindingTimes)
    {
        Angle = angle;
        RegularAverageDepth = regularAverageDepth;
        DefectDepth = defectDepth;
        FinalGrindDepth = finalGrindDepth;
        GrindingTimes = grindingTimes;
    }

    public int Angle { get; }

    public double RegularAverageDepth { get; }

    public double DefectDepth { get; }

    public double FinalGrindDepth { get; }

    /// <summary>
    /// 兼容原有调用方；合并功能启用后返回最终建议打磨深度。  
    /// </summary>
    public double AverageGrindDepth => FinalGrindDepth;

    public int GrindingTimes { get; }
}
