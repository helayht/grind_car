using System.Collections.Generic;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 一次测量运行流程结束后的汇总结果。
/// </summary>
/// <param name="SampleCount">本轮累计的单次测量次数。</param>
/// <param name="Results">每个角度对应的平均打磨深度与打磨次数。</param>
public sealed record MeasurementGrindingWorkflowResult(
    int SampleCount,
    IReadOnlyList<MeasurementGrindingTimesResult> Results);
