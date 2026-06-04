using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Measurement;
using Xunit;

namespace GrindCar.Tests;

public class MeasurementParameterServiceTests
{
    [Fact]
    public async Task WriteGrindingTimesAsync_UsesConfirmedGrindingTimes()
    {
        var plcClient = new FakePlcClient();
        var service = new MeasurementParameterService((ipAddress, port) => plcClient);
        var results = new[]
        {
            new MeasurementGrindingTimesResult(0, 0.01, 7)
        };

        await service.WriteGrindingTimesAsync("127.0.0.1", 502, results);

        ushort expectedAddress = MotorParameterDefinitions.MeasurementGrindingTimesAddresses[0];
        Assert.True(plcClient.ConnectCalled);
        Assert.True(plcClient.DisconnectCalled);
        Assert.Equal(7, plcClient.WrittenInt32Values[expectedAddress]);
    }

    [Fact]
    public async Task RunMeasurementWorkflowAsync_TwoM60Triggers_CapturesTwoDevicesAndWritesM61Twice()
    {
        var plcClient = new FakePlcClient(
            measurementFinishedValues: new[] { false, false, true },
            captureTriggerValues: new[] { true, true });
        var devices = new[]
        {
            new ConfiguredPointCloudDevice("SN-LEFT", PointCloudDeviceSide.Left),
            new ConfiguredPointCloudDevice("SN-RIGHT", PointCloudDeviceSide.Right)
        };
        var capturedSerialNumbers = new List<string>();
        bool measurementEnded = false;
        var service = new MeasurementParameterService(
            (ipAddress, port) => plcClient,
            () => devices,
            () => new PointCloudCaptureSettings(1.0, 10),
            (device, settings) =>
            {
                capturedSerialNumbers.Add(device.SerialNumber);
                return new[] { new RailProfilePoint(capturedSerialNumbers.Count, 1.0) };
            },
            (angles, representativePoints) =>
                new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.1)).ToArray(),
                    representativePoints));

        MeasurementGrindingWorkflowResult result = await service.RunMeasurementWorkflowAsync(
            "127.0.0.1",
            502,
            measurementEnded: () => measurementEnded = true);

        Assert.True(measurementEnded);
        Assert.Equal(1, result.SampleCount);
        Assert.Equal(new[] { "SN-LEFT", "SN-RIGHT" }, capturedSerialNumbers);
        Assert.Equal(1, plcClient.CountWrites(MotorParameterDefinitions.MeasurementMotionStartAddress, true));
        Assert.Equal(2, plcClient.CountWrites(MotorParameterDefinitions.MeasurementCurrentProfileCompletedAddress, true));
    }

    [Fact]
    public async Task RunMeasurementWorkflowAsync_M62AfterOneDevice_ThrowsIncompleteSampleError()
    {
        var plcClient = new FakePlcClient(
            measurementFinishedValues: new[] { false, true },
            captureTriggerValues: new[] { true });
        var devices = new[]
        {
            new ConfiguredPointCloudDevice("SN-LEFT", PointCloudDeviceSide.Left),
            new ConfiguredPointCloudDevice("SN-RIGHT", PointCloudDeviceSide.Right)
        };
        var service = new MeasurementParameterService(
            (ipAddress, port) => plcClient,
            () => devices,
            () => new PointCloudCaptureSettings(1.0, 10),
            (device, settings) => new[] { new RailProfilePoint(1.0, 1.0) },
            (angles, representativePoints) =>
                new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.1)).ToArray(),
                    representativePoints));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunMeasurementWorkflowAsync("127.0.0.1", 502));

        Assert.Contains("未采集到任何单次测量结果", exception.Message);
        Assert.Equal(1, plcClient.CountWrites(MotorParameterDefinitions.MeasurementCurrentProfileCompletedAddress, true));
    }

    private sealed class FakePlcClient : IPlcClient
    {
        private readonly Queue<bool> _measurementFinishedValues;
        private readonly Queue<bool> _captureTriggerValues;

        public FakePlcClient()
            : this(Array.Empty<bool>(), Array.Empty<bool>())
        {
        }

        public FakePlcClient(IEnumerable<bool> measurementFinishedValues, IEnumerable<bool> captureTriggerValues)
        {
            _measurementFinishedValues = new Queue<bool>(measurementFinishedValues);
            _captureTriggerValues = new Queue<bool>(captureTriggerValues);
        }

        public bool IsConnected => ConnectCalled;

        public bool ConnectCalled { get; private set; }

        public bool DisconnectCalled { get; private set; }

        public Dictionary<ushort, int> WrittenInt32Values { get; } = new();

        public List<(ushort Address, bool Value)> WrittenCoilValues { get; } = new();

        public Task ConnectAsync()
        {
            ConnectCalled = true;
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            DisconnectCalled = true;
        }

        public int ReadInt32(ushort startAddress)
        {
            throw new NotSupportedException();
        }

        public bool ReadSingleCoil(ushort coilAddress)
        {
            if (coilAddress == MotorParameterDefinitions.MeasurementMotionFinishedAddress)
            {
                return _measurementFinishedValues.Count > 0 && _measurementFinishedValues.Dequeue();
            }

            if (coilAddress == MotorParameterDefinitions.MeasurementProfileCaptureStartAddress)
            {
                return _captureTriggerValues.Count > 0 && _captureTriggerValues.Dequeue();
            }

            throw new NotSupportedException();
        }

        public void WriteInt32(ushort startAddress, int value)
        {
            WrittenInt32Values[startAddress] = value;
        }

        public void WriteInt16(ushort address, short value)
        {
            throw new NotSupportedException();
        }

        public Task WriteSingleCoilAsync(ushort coilAddress, bool value)
        {
            WrittenCoilValues.Add((coilAddress, value));
            return Task.CompletedTask;
        }

        public int CountWrites(ushort address, bool value)
        {
            return WrittenCoilValues.Count(item => item.Address == address && item.Value == value);
        }

        public void Dispose()
        {
        }
    }
}
