using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Services;
using GrindCar.Services.Telemetry;

namespace GrindCar.ViewModels;

public sealed class DashboardTelemetryViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly SharedPlcConnectionService _connection;
    private readonly IDashboardTelemetryService _service;
    private readonly SynchronizationContext? _context;
    private CancellationTokenSource? _cancellation;
    private Task _pollTask = Task.CompletedTask;
    private long _generation;
    private DashboardTelemetrySnapshot _snapshot = DashboardTelemetrySnapshot.Unknown;
    public bool? MeasurementRunning => _snapshot.MeasurementRunning;
    public string MeasurementStateText => MeasurementRunning.HasValue ? (MeasurementRunning.Value ? "测量运行中" : "已停止") : "未知";
    public string BatteryLevelText => _snapshot.BatteryLevel is >= 0 and <= 100 ? $"{_snapshot.BatteryLevel}{MotorParameterDefinitions.UnitPercent}" : "--";
    public string BatteryStateText => !_snapshot.BatteryLevel.HasValue ? "暂无有效数据" :
        _snapshot.BatteryLevel is >= 0 and <= 100 ? "实时电池电量" : "电量数据异常";
    public string Pressure1Text => _snapshot.Pressure1.HasValue ? $"{_snapshot.Pressure1} {MotorParameterDefinitions.UnitNewton}" : "--";
    public string Pressure2Text => _snapshot.Pressure2.HasValue ? $"{_snapshot.Pressure2} {MotorParameterDefinitions.UnitNewton}" : "--";

    public DashboardTelemetryViewModel(SharedPlcConnectionService connection, IDashboardTelemetryService? service = null)
    {
        _connection = connection;
        _service = service ?? new DashboardTelemetryService(() => connection.CreateClientLease());
        _context = SynchronizationContext.Current;
        _connection.PropertyChanged += ConnectionChanged;
    }

    public void StartPolling()
    {
        if (_cancellation != null) return;
        _cancellation = new CancellationTokenSource();
        _pollTask = PollAsync(_cancellation.Token);
    }

    public async Task StopPollingAsync()
    {
        CancellationTokenSource? cancellation = _cancellation;
        if (cancellation == null) return;
        cancellation.Cancel();
        Interlocked.Increment(ref _generation);
        Publish(DashboardTelemetrySnapshot.Unknown);
        await _pollTask;
        cancellation.Dispose();
        if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null;
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_connection.IsConnected)
                {
                    long generation = Interlocked.Read(ref _generation);
                    DashboardTelemetrySnapshot snapshot;
                    try { snapshot = await _service.ReadAsync(token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception) { snapshot = DashboardTelemetrySnapshot.Unknown; }
                    if (!token.IsCancellationRequested && generation == Interlocked.Read(ref _generation) && _connection.IsConnected)
                        Publish(snapshot);
                }
                else Publish(DashboardTelemetrySnapshot.Unknown);
                await Task.Delay(MotorParameterDefinitions.DashboardPollIntervalMs, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Publish(DashboardTelemetrySnapshot snapshot)
    {
        _snapshot = snapshot;
        Notify(nameof(MeasurementRunning)); Notify(nameof(MeasurementStateText));
        Notify(nameof(BatteryLevelText)); Notify(nameof(BatteryStateText));
        Notify(nameof(Pressure1Text)); Notify(nameof(Pressure2Text));
    }

    private void ConnectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        long generation = Interlocked.Increment(ref _generation);
        void Clear()
        {
            if (generation == Interlocked.Read(ref _generation)) Publish(DashboardTelemetrySnapshot.Unknown);
        }
        if (_context == null || SynchronizationContext.Current == _context) Clear();
        else _context.Post(_ => Clear(), null);
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _connection.PropertyChanged -= ConnectionChanged;
    }
}
