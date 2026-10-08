using System.Collections.Generic;
using System;
using System.Linq;
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
        MaximumDropProfileResult? maximumDropProfile,
        IReadOnlyList<MeasurementRepresentativeProfile>? representativeProfiles = null)
    {
        SampleCount = sampleCount;
        Results = results;
        MaximumDropProfile = maximumDropProfile;
        RepresentativeProfiles = Array.AsReadOnly(
            (representativeProfiles ?? Array.Empty<MeasurementRepresentativeProfile>()).ToArray());
    }

    public int SampleCount { get; }

    public IReadOnlyList<MeasurementRepresentativeProfile> RepresentativeProfiles { get; }

    public IReadOnlyList<MeasurementGrindingTimesResult> Results { get; }

    public MaximumDropProfileResult? MaximumDropProfile { get; }
}
