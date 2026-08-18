using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 一次测量运行流程结束后的汇总结果。
/// </summary>
public sealed record MeasurementGrindingWorkflowResult
{
    public MeasurementGrindingWorkflowResult(
        int sampleCount,
        IReadOnlyList<MeasurementGrindingTimesResult> results)
        : this(sampleCount, results, null)
    {
    }

    public MeasurementGrindingWorkflowResult(
        int sampleCount,
        IReadOnlyList<MeasurementGrindingTimesResult> results,
        MaximumDropProfileResult? maximumDropProfile)
    {
        SampleCount = sampleCount;
        Results = results;
        MaximumDropProfile = maximumDropProfile;
    }

    public int SampleCount { get; }

    public IReadOnlyList<MeasurementGrindingTimesResult> Results { get; }

    public MaximumDropProfileResult? MaximumDropProfile { get; }
}
