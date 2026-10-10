using System;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;

namespace GrindCar.Services.Telemetry;

public sealed record DashboardTelemetrySnapshot(bool? MeasurementRunning, int? BatteryLevel, int? Pressure1, int? Pressure2)
{
    public static DashboardTelemetrySnapshot Unknown { get; } = new(null, null, null, null);
}

public interface IDashboardTelemetryService
{
    Task<DashboardTelemetrySnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class DashboardTelemetryService : IDashboardTelemetryService
{
    private readonly Func<IPlcClient> _clientFactory;
    public DashboardTelemetryService(Func<IPlcClient> clientFactory) => _clientFactory = clientFactory;

    public Task<DashboardTelemetrySnapshot> ReadAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        using IPlcClient client = _clientFactory();
        bool? running = Read(() => client.ReadSingleCoil(MotorParameterDefinitions.MeasurementRunningAddress), cancellationToken);
        int? battery = Read(() => client.ReadInt32(MotorParameterDefinitions.BatteryLevelAddress), cancellationToken);
        int? pressure1 = Read(() => client.ReadInt32(MotorParameterDefinitions.PressureSensor1Address), cancellationToken);
        int? pressure2 = Read(() => client.ReadInt32(MotorParameterDefinitions.PressureSensor2Address), cancellationToken);
        return new DashboardTelemetrySnapshot(running, battery, pressure1, pressure2);
    }, cancellationToken);

    private static T? Read<T>(Func<T> read, CancellationToken token) where T : struct
    {
        token.ThrowIfCancellationRequested();
        try { return read(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }
}
