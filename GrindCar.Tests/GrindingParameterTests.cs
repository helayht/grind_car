using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.Grinding;
using GrindCar.Services;
using GrindCar.Services.Grinding;
using GrindCar.Services.Measurement;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class GrindingParameterTests
{
    internal static GrindingSettings CreateSettings() => new()
    {
        Basic = new() { ["Start"] = -1.25m, ["End"] = 2m, ["Smooth"] = 0.1m, ["Speed"] = 3.5m },
        Angles = MotorParameterDefinitions.MeasurementGrindingAngles.Select((angle, index) => new GrindingAngleSettings
        {
            Number = index + 1, Angle = angle,
            Values = MotorParameterDefinitions.GrindingAngleParameters.ToDictionary(definition => definition.Key, _ => 1m)
        }).ToList()
    };

    [Fact]
    public void Addresses_AllParameters_HaveCorrectStrideAndNoOverlaps()
    {
        List<GrindingRegisterWrite> writes = GrindingSettingsValidator.BuildAll(CreateSettings());
        Assert.Equal(213, writes.Count);
        Assert.Equal(8258, MotorParameterDefinitions.GrindingRunningAddress);
        var occupied = new HashSet<int>();
        foreach (GrindingRegisterWrite write in writes)
        {
            Assert.True(occupied.Add(write.Address));
            if (!write.IsInt16) Assert.True(occupied.Add(write.Address + 1));
        }
        for (int address = 1800; address <= 1837; address++) Assert.DoesNotContain(address, occupied);
        Assert.Contains(writes, write => write.Address == 1236 && write.Value == 900000);
        Assert.Contains(writes, write => write.Address == 1718 && write.IsInt16);
        Assert.Contains(writes, write => write.Address == 2536);
        Assert.Contains(writes, write => write.Address == 1200 && write.Value == -350000);
    }

    [Fact]
    public async Task WriteAll_Valid_WritesScaledParametersWithoutTimesOrCoils()
    {
        var client = new FakeClient();
        await new GrindingParameterService(() => client).WriteAllAsync(CreateSettings(), CancellationToken.None);
        Assert.Equal(213, client.Writes.Count);
        Assert.Contains((1180, -125000, false), client.Writes);
        Assert.Contains((1134, 3500, false), client.Writes);
        Assert.Contains((1188, 10000, false), client.Writes);
        Assert.Empty(client.Coils);
        Assert.All(client.Reads, address => Assert.Equal(8258, address));
    }

    [Fact]
    public async Task WriteAngle_Selected_WritesOnlyElevenParameters()
    {
        var client = new FakeClient();
        await new GrindingParameterService(() => client).WriteAngleAsync(CreateSettings().Angles[1], CancellationToken.None);
        Assert.Equal(11, client.Writes.Count);
        Assert.Equal((1202, -200000, false), client.Writes[0]);
        Assert.Contains((1701, 1, true), client.Writes);
        Assert.DoesNotContain(client.Writes, write => write.Address < 1200 || write.Address is >= 1800 and <= 1837);
    }

    [Theory]
    [InlineData("Speed", "0")]
    [InlineData("Smooth", "-1")]
    [InlineData("Start", "3")]
    [InlineData("End", "99999999999999999999999")]
    public async Task WriteAll_InvalidBasic_PerformsNoIo(string key, string text)
    {
        GrindingSettings settings = CreateSettings();
        settings.Basic[key] = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var client = new FakeClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GrindingParameterService(() => client).WriteAllAsync(settings, CancellationToken.None));
        Assert.Empty(client.Writes);
        Assert.Empty(client.Reads);
    }

    [Theory]
    [InlineData("Torque", "1.2")]
    [InlineData("Torque", "32768")]
    [InlineData("Feed", "-0.1")]
    [InlineData("PositionPressure1", "1.1")]
    [InlineData("GrindingPressure2", "-1")]
    public async Task WriteAll_InvalidLastAngle_PerformsNoIo(string key, string text)
    {
        GrindingSettings settings = CreateSettings();
        settings.Angles[18].Values[key] = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var client = new FakeClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GrindingParameterService(() => client).WriteAllAsync(settings, CancellationToken.None));
        Assert.Empty(client.Writes);
        Assert.Empty(client.Reads);
    }

    [Fact]
    public async Task WriteAll_MiddleFailure_StopsAndReportsPartialWrite()
    {
        var client = new FakeClient { FailAt = 2 };
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GrindingParameterService(() => client).WriteAllAsync(CreateSettings(), CancellationToken.None));
        Assert.Equal(2, client.Writes.Count);
        Assert.Contains("D1188", error.Message);
        Assert.Contains("2/213", error.Message);
        Assert.Contains("部分写入", error.Message);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task WriteAll_RunningOrReadFailure_WritesNothing(bool running, bool failRead)
    {
        var client = new FakeClient { Running = running, FailRead = failRead };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GrindingParameterService(() => client).WriteAllAsync(CreateSettings(), CancellationToken.None));
        Assert.Empty(client.Writes);
    }

    [Fact]
    public void Store_RoundTripAndCorruption_ValidatesFixedAngles()
    {
        string folder = Path.Combine(Path.GetTempPath(), "grinding-tests-" + Guid.NewGuid());
        string path = Path.Combine(folder, "settings.json");
        try
        {
            var store = new GrindingSettingsStore(path);
            Assert.Null(store.Load());
            store.Save(CreateSettings());
            store.Save(CreateSettings());
            GrindingSettings loaded = store.Load()!;
            Assert.Equal(-1.25m, loaded.Basic["Start"]);
            Assert.Equal(19, loaded.Angles.Count);
            Assert.Single(Directory.GetFiles(folder));
            loaded.Angles[0].Angle = 0;
            Assert.Throws<InvalidOperationException>(() => store.Save(loaded));
            Assert.Equal(-35, store.Load()!.Angles[0].Angle);
            File.WriteAllText(path, "{broken");
            Assert.Throws<InvalidOperationException>(() => store.Load());
            File.WriteAllText(path, "{\"Version\":1,\"Angles\":[null]}");
            Assert.Throws<InvalidOperationException>(() => store.Load());
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("")]
    public void Field_InvalidNumber_IsRejected(string input)
    {
        var field = new GrindingFieldViewModel(MotorParameterDefinitions.GrindingBasicParameters[0]) { Text = input };
        Assert.Throws<InvalidOperationException>(() => field.Parse());
    }

    [Fact]
    public async Task ViewModel_NoConnection_StartsEmptyShowsUnknownAndPreservesResults()
    {
        using var connection = new SharedPlcConnectionService("127.0.0.1", 502, 1);
        var measurement = new MainWindowMeasurementViewModel(new MeasurementParameterService(), connection, "127.0.0.1", 502);
        var client = new FakeClient();
        using var vm = new GrindingParametersViewModel(new GrindingParameterService(() => client),
            new GrindingSettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), connection, measurement);
        Assert.All(vm.BasicFields.Concat(vm.Angles.SelectMany(angle => angle.Fields)), field => Assert.Equal("", field.Text));
        Assert.All(vm.Angles, angle => Assert.Equal("未测量", angle.Times));
        Assert.False(vm.CanWrite);
        vm.StartPolling();
        await vm.StopPollingAsync();
        Assert.Contains("未知", vm.RunningStatus);
        Assert.Empty(client.Reads);
        await vm.WriteAsync(false);
        Assert.Empty(client.Writes);
        vm.SetConfirmedResults(new[] { new MeasurementGrindingTimesResult(-35, 0.1, 0, 0.1, 2) }, "写入失败/可能部分写入");
        Assert.Equal("2", vm.Angles[0].Times);
        Assert.Contains("失败", vm.Angles[0].TimesWriteState);
        vm.SetConfirmedResults(new[] { new MeasurementGrindingTimesResult(-35, 0.1, 0, 0.1, 2) }, "写入成功");
        Assert.Equal("写入成功", vm.Angles[0].TimesWriteState);
        vm.StartPolling();
        await vm.StopPollingAsync();
    }

    private sealed class FakeClient : IPlcClient
    {
        public bool IsConnected => true;
        public bool Running { get; set; }
        public bool FailRead { get; set; }
        public int FailAt { get; set; } = -1;
        public List<(int Address, int Value, bool Int16)> Writes { get; } = new();
        public List<ushort> Reads { get; } = new();
        public List<ushort> Coils { get; } = new();
        public Task ConnectAsync() => Task.CompletedTask;
        public void Disconnect() { }
        public void Dispose() { }
        public int ReadInt32(ushort address) => throw new NotSupportedException();
        public bool ReadSingleCoil(ushort address)
        {
            Reads.Add(address);
            if (FailRead) throw new IOException("模拟读失败");
            return Running;
        }
        public void WriteInt32(ushort address, int value) => Write(address, value, false);
        public void WriteInt16(ushort address, short value) => Write(address, value, true);
        private void Write(ushort address, int value, bool int16)
        {
            if (Writes.Count == FailAt) throw new IOException("模拟写失败");
            Writes.Add((address, value, int16));
        }
        public Task WriteSingleCoilAsync(ushort address, bool value) { Coils.Add(address); return Task.CompletedTask; }
    }
}
