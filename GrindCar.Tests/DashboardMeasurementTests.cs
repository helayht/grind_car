using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.Measurement;
using GrindCar.Services;
using GrindCar.Services.Measurement;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Telemetry;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class DashboardMeasurementTests
{
    [Fact]
    public async Task Telemetry_ReadsCorrectAddressesAndRetainsSignedPressure()
    {
        var client = new FakeClient();
        var service = new DashboardTelemetryService(() => client);
        DashboardTelemetrySnapshot data = await service.ReadAsync(CancellationToken.None);
        Assert.Equal(new DashboardTelemetrySnapshot(false, 82, -5, 120), data);
        Assert.Equal(new ushort[] { 8257, 2800, 2802, 2804 }, client.Reads);
        Assert.Empty(client.Writes);
    }

    [Theory]
    [InlineData(8257)]
    [InlineData(2800)]
    [InlineData(2802)]
    [InlineData(2804)]
    public async Task Telemetry_SingleFailure_OnlyClearsFailedField(int failedAddress)
    {
        var client = new FakeClient { FailReadAddress = failedAddress };
        DashboardTelemetrySnapshot data = await new DashboardTelemetryService(() => client).ReadAsync(CancellationToken.None);
        Assert.Equal(failedAddress == 8257 ? null : false, data.MeasurementRunning);
        Assert.Equal(failedAddress == 2800 ? null : 82, data.BatteryLevel);
        Assert.Equal(failedAddress == 2802 ? null : -5, data.Pressure1);
        Assert.Equal(failedAddress == 2804 ? null : 120, data.Pressure2);
    }

    [Fact]
    public async Task WriteParameters_Valid_WritesFiveScaledValuesWithoutStartingMotion()
    {
        var client = new FakeClient();
        var service = new MeasurementParameterService((_, _) => client);
        await service.WriteMeasurementParametersAsync("", 0, new(new(-1.25, 2.5, -3.125, 4.5), 6.75));
        Assert.Equal(new[] { (1140, -125000), (1142, 250000), (1144, -31250), (1146, 45000), (1132, 6750) }, client.Writes);
        Assert.Empty(client.Coils);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(double.MaxValue)]
    public async Task WriteParameters_InvalidSpeed_PerformsNoIo(double speed)
    {
        var client = new FakeClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MeasurementParameterService((_, _) => client)
            .WriteMeasurementParametersAsync("", 0, new(new(0, 1, 2, 3), speed)));
        Assert.Empty(client.Reads);
        Assert.Empty(client.Writes);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(2, 1)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(0, 1e20)]
    public async Task WriteParameters_InvalidPositions_PerformsNoIo(double start, double end)
    {
        var client = new FakeClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MeasurementParameterService((_, _) => client)
            .WriteMeasurementParametersAsync("", 0, new(new(start, end, 2, 3), 1)));
        Assert.Empty(client.Reads);
        Assert.Empty(client.Writes);
    }

    [Fact]
    public async Task WriteParameters_PartialFailure_StopsAndReports()
    {
        var client = new FakeClient { FailWriteAt = 2 };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new MeasurementParameterService((_, _) => client)
            .WriteMeasurementParametersAsync("", 0, new(new(0, 1, 2, 3), 1)));
        Assert.Equal(2, client.Writes.Count);
        Assert.Contains("D1144", error.Message);
        Assert.Contains("2/5", error.Message);
    }

    [Theory]
    [InlineData(true, -1)]
    [InlineData(false, 8257)]
    public async Task WriteParameters_RunningOrUnknown_WritesNothing(bool running, int failedAddress)
    {
        var client = new FakeClient { Running = running, FailReadAddress = failedAddress };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MeasurementParameterService((_, _) => client)
            .WriteMeasurementParametersAsync("", 0, new(new(0, 1, 2, 3), 1)));
        Assert.Empty(client.Writes);
    }

    [Fact]
    public void PositionStore_RoundTripAndCorruption_DoNotSilentlyDefault()
    {
        string directory = Path.Combine(Path.GetTempPath(), "measurement-test-" + Guid.NewGuid());
        string path = Path.Combine(directory, "positions.json");
        try
        {
            var store = new MeasurementPositionSettingsStore(path);
            Assert.Null(store.Load());
            var positions = new MeasurementPositions(-1, 3, -2.5, 10);
            store.Save(positions);
            store.Save(positions);
            Assert.Equal(positions, store.Load());
            Assert.Single(Directory.GetFiles(directory));
            Assert.DoesNotContain("Speed", File.ReadAllText(path));
            Assert.Throws<InvalidOperationException>(() => store.Save(new(4, 3, 0, 0)));
            Assert.Equal(positions, store.Load());
            foreach (string json in new[] { "{broken", "{}", "{\"Version\":1,\"Start\":0,\"End\":1}" })
            {
                File.WriteAllText(path, json);
                Assert.Throws<InvalidOperationException>(() => store.Load());
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ViewModel_SpeedAndSave_UseOneInputAndDoNotWriteOnSave()
    {
        string directory = Path.Combine(Path.GetTempPath(), "measurement-vm-" + Guid.NewGuid());
        var client = new FakeClient();
        using var connection = Connected(client);
        using var telemetry = new DashboardTelemetryViewModel(connection, new ImmediateTelemetry());
        var positions = new MeasurementPositionSettingsStore(Path.Combine(directory, "positions.json"));
        var capture = new PointCloudCaptureSettingsStore(Path.Combine(directory, "capture.json"));
        try
        {
            using var vm = new MainWindowMeasurementViewModel(new MeasurementParameterService((_, _) => client), connection, "", 0, capture, positions, telemetry)
            {
                StartPositionText = "0", EndPositionText = "1", ProfilerPositionText = "2",
                AvoidancePositionText = "3", CarSpeedText = "6", ProfileCountText = "100"
            };
            Assert.False(vm.CanWriteMeasurementParameters);
            vm.SaveMeasurementPositions();
            vm.SavePointCloudCaptureSettings();
            Assert.Empty(client.Writes);
            Assert.Equal(6, capture.Load()!.SpeedMetersPerMinute);
            Assert.Equal("10 Hz", vm.CalculatedFrameRateText);
            telemetry.StartPolling();
            Assert.True(vm.CanWriteMeasurementParameters);
            vm.SetWorkflowActive(true);
            Assert.False(vm.CanWriteMeasurementParameters);
            vm.SetWorkflowActive(false);
            vm.SetGrindingParametersBusy(true);
            Assert.False(vm.CanWriteMeasurementParameters);
            vm.SetGrindingParametersBusy(false);
            await vm.WriteMeasurementParametersAsync();
            Assert.Contains((1132, 6000), client.Writes);
            Assert.Empty(client.Coils);
            await telemetry.StopPollingAsync();
            Assert.False(vm.CanWriteMeasurementParameters);
            using var restored = new MainWindowMeasurementViewModel(new MeasurementParameterService(), connection, "", 0, capture, positions);
            Assert.Equal("2", restored.ProfilerPositionText);
            Assert.Equal("3", restored.AvoidancePositionText);
            Assert.Equal("6", restored.CarSpeedText);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Polling_DisconnectAndOldConnectionResult_AreDiscarded()
    {
        using var connection = Connected(new FakeClient());
        var service = new DelayedTelemetry();
        using var vm = new DashboardTelemetryViewModel(connection, service);
        vm.StartPolling();
        connection.Disconnect();
        Assert.Equal("--", vm.BatteryLevelText);
        SetClient(connection, new FakeClient());
        service.Completion.SetResult(new(false, 99, 1, 2));
        await Task.Delay(100);
        Assert.Equal("--", vm.BatteryLevelText);
        await vm.StopPollingAsync();
        int count = service.ReadCount;
        await Task.Delay(550);
        Assert.Equal(count, service.ReadCount);
        using var restarted = new DashboardTelemetryViewModel(connection, new ImmediateTelemetry(new(false, 101, -10, null)));
        restarted.StartPolling();
        Assert.Equal("--", restarted.BatteryLevelText);
        Assert.Equal("电量数据异常", restarted.BatteryStateText);
        Assert.Equal("-10 N", restarted.Pressure1Text);
        Assert.Equal("--", restarted.Pressure2Text);
        await restarted.StopPollingAsync();
        Assert.Equal("未知", restarted.MeasurementStateText);
    }

    private static SharedPlcConnectionService Connected(IPlcClient client)
    {
        var connection = new SharedPlcConnectionService("127.0.0.1", 502, 1);
        SetClient(connection, client);
        return connection;
    }
    private static void SetClient(SharedPlcConnectionService connection, IPlcClient client) =>
        typeof(SharedPlcConnectionService).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(connection, client);

    private sealed class ImmediateTelemetry : IDashboardTelemetryService
    {
        private readonly DashboardTelemetrySnapshot _snapshot;
        public ImmediateTelemetry(DashboardTelemetrySnapshot? snapshot = null) => _snapshot = snapshot ?? new(false, 80, 2, 3);
        public Task<DashboardTelemetrySnapshot> ReadAsync(CancellationToken token) => Task.FromResult(_snapshot);
    }
    private sealed class DelayedTelemetry : IDashboardTelemetryService
    {
        public TaskCompletionSource<DashboardTelemetrySnapshot> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReadCount { get; private set; }
        public Task<DashboardTelemetrySnapshot> ReadAsync(CancellationToken token) { ReadCount++; return Completion.Task; }
    }
    private sealed class FakeClient : IPlcClient
    {
        public bool IsConnected => true;
        public bool Running { get; set; }
        public int FailReadAddress { get; set; } = -1;
        public int FailWriteAt { get; set; } = -1;
        public List<ushort> Reads { get; } = new();
        public List<(int Address, int Value)> Writes { get; } = new();
        public List<ushort> Coils { get; } = new();
        public Task ConnectAsync() => Task.CompletedTask;
        public void Disconnect() { }
        public void Dispose() { }
        public int ReadInt32(ushort address)
        {
            Read(address);
            return address switch { 2800 => 82, 2802 => -5, 2804 => 120, _ => throw new InvalidOperationException() };
        }
        public bool ReadSingleCoil(ushort address) { Read(address); return Running; }
        private void Read(ushort address)
        {
            Reads.Add(address);
            if (address == FailReadAddress) throw new IOException("模拟读取失败");
        }
        public void WriteInt32(ushort address, int value)
        {
            if (Writes.Count == FailWriteAt) throw new IOException("模拟写入失败");
            Writes.Add((address, value));
        }
        public void WriteInt16(ushort address, short value) => throw new NotSupportedException();
        public Task WriteSingleCoilAsync(ushort address, bool value) { Coils.Add(address); return Task.CompletedTask; }
    }
}
