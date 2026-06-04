using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 测量参数写入服务。
/// </summary>
public interface IMeasurementParameterService
{
    Task WriteMeasurementRangeAsync(string ipAddress, int port, double startPosition, double endPosition);
    Task WriteGrindingRangeAsync(string ipAddress, int port, double startPosition, double endPosition);

    Task StartMeasurementMotionAsync(string ipAddress, int port);
    Task StartGrindingMotionAsync(string ipAddress, int port);
    Task WriteGrindingTimesAsync(
        string ipAddress,
        int port,
        IReadOnlyList<MeasurementGrindingTimesResult> results);

    Task<MeasurementGrindingWorkflowResult> RunMeasurementWorkflowAsync(
        string ipAddress,
        int port,
        IProgress<string>? progress = null,
        Action? measurementEnded = null,
        CancellationToken cancellationToken = default);
}
