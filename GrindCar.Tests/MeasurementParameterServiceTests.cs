using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Measurement;
using GrindCar.Services.Rail.Core;
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
    public async Task RunMeasurementWorkflowAsync_TwoM60RisingEdges_CapturesTwoDevicesAndWritesM62Twice()
    {
        var plcClient = new FakePlcClient(
            positionCompletedValues: new[] { false, false, false, true },
            captureTriggerValues: new[] { true, false, true });
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
            (device, settings, archiveContext) =>
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
    public async Task RunMeasurementWorkflowAsync_M60StaysTrue_OnlyCapturesOnce()
    {
        var plcClient = new FakePlcClient(
            positionCompletedValues: new[] { false, false, true },
            captureTriggerValues: new[] { true, true });
        var devices = new[]
        {
            new ConfiguredPointCloudDevice("SN-LEFT", PointCloudDeviceSide.Left),
            new ConfiguredPointCloudDevice("SN-RIGHT", PointCloudDeviceSide.Right)
        };
        int captureCount = 0;
        var service = new MeasurementParameterService(
            (ipAddress, port) => plcClient,
            () => devices,
            () => new PointCloudCaptureSettings(1.0, 10),
            (device, settings, archiveContext) =>
            {
                captureCount++;
                return new[] { new RailProfilePoint(1.0, 1.0) };
            },
            (angles, representativePoints) =>
                new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.1)).ToArray(),
                    representativePoints));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunMeasurementWorkflowAsync("127.0.0.1", 502));

        Assert.Contains("未采集到任何单次测量结果", exception.Message);
        Assert.Equal(1, captureCount);
        Assert.Equal(1, plcClient.CountWrites(MotorParameterDefinitions.MeasurementCurrentProfileCompletedAddress, true));
    }

    [Fact]
    public async Task RunMeasurementWorkflowAsync_M61AfterOneDevice_ThrowsIncompleteSampleError()
    {
        var plcClient = new FakePlcClient(
            positionCompletedValues: new[] { false, true },
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
            (device, settings, archiveContext) => new[] { new RailProfilePoint(1.0, 1.0) },
            (angles, representativePoints) =>
                new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.1)).ToArray(),
                    representativePoints));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunMeasurementWorkflowAsync("127.0.0.1", 502));

        Assert.Contains("未采集到任何单次测量结果", exception.Message);
        Assert.Equal(1, plcClient.CountWrites(MotorParameterDefinitions.MeasurementCurrentProfileCompletedAddress, true));
    }

    [Fact]
    public async Task RunMeasurementWorkflowAsync_TwoM60Triggers_PassesArchiveContexts()
    {
        var plcClient = new FakePlcClient(
            positionCompletedValues: new[] { false, false, false, true },
            captureTriggerValues: new[] { true, false, true });
        var devices = new[]
        {
            new ConfiguredPointCloudDevice("SN-LEFT", PointCloudDeviceSide.Left),
            new ConfiguredPointCloudDevice("SN-RIGHT", PointCloudDeviceSide.Right)
        };
        var archiveContexts = new List<MeasurementPointCloudArchiveContext>();
        var service = new MeasurementParameterService(
            (ipAddress, port) => plcClient,
            () => devices,
            () => new PointCloudCaptureSettings(1.0, 10),
            (device, settings, archiveContext) =>
            {
                if (archiveContext != null)
                {
                    archiveContexts.Add(archiveContext);
                }

                return new[] { new RailProfilePoint(archiveContexts.Count, 1.0) };
            },
            (angles, representativePoints) =>
                new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.1)).ToArray(),
                    representativePoints));

        MeasurementGrindingWorkflowResult result = await service.RunMeasurementWorkflowAsync("127.0.0.1", 502);

        Assert.Equal(1, result.SampleCount);
        Assert.Equal(2, archiveContexts.Count);
        Assert.Equal(1, archiveContexts[0].SampleIndex);
        Assert.Equal(1, archiveContexts[0].DeviceIndex);
        Assert.Equal("SN-LEFT", archiveContexts[0].SerialNumber);
        Assert.Equal(PointCloudDeviceSide.Left, archiveContexts[0].Side);
        Assert.Equal(1, archiveContexts[1].SampleIndex);
        Assert.Equal(2, archiveContexts[1].DeviceIndex);
        Assert.Equal("SN-RIGHT", archiveContexts[1].SerialNumber);
        Assert.Equal(PointCloudDeviceSide.Right, archiveContexts[1].Side);
    }

    [Fact]
    public async Task RunMeasurementWorkflowAsync_KeepsRegularAverageAndGlobalMaximumDrop()
    {
        var plcClient = new FakePlcClient(
            positionCompletedValues: new[]
            {
                false, false, false, false, false, false, false, true
            },
            captureTriggerValues: new[]
            {
                true, false, true, false, true, false, true
            });
        var devices = new[]
        {
            new ConfiguredPointCloudDevice("SN-LEFT", PointCloudDeviceSide.Left),
            new ConfiguredPointCloudDevice("SN-RIGHT", PointCloudDeviceSide.Right)
        };
        int regularCalculationCount = 0;
        var defectAngleCalls = new List<(double Marker, int[] Angles)>();
        var service = new MeasurementParameterService(
            (ipAddress, port) => plcClient,
            configuredDeviceProvider: () => devices,
            captureSettingsProvider: () => new PointCloudCaptureSettings(1.0, 10),
            grindDepthCalculator: (angles, points) =>
            {
                regularCalculationCount++;
                double depth = regularCalculationCount == 1 ? 0.1 : 0.3;
                return new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, depth)).ToArray(),
                    points);
            },
            deviceAnalysisCapture: (device, settings, archiveContext) =>
            {
                int sampleIndex = archiveContext!.SampleIndex;
                double maximumDropDepth = device.Side == PointCloudDeviceSide.Left
                    ? (sampleIndex == 1 ? 1.0 : 3.0)
                    : (sampleIndex == 1 ? 0.5 : 2.0);
                double defectMarker = device.Side == PointCloudDeviceSide.Left
                    ? (sampleIndex == 1 ? 0.1 : 0.4)
                    : (sampleIndex == 1 ? 0.1 : 0.25);
                var maximumDropProfile = new MaximumDropProfileResult(
                    device.Side,
                    sampleIndex,
                    sampleIndex,
                    maximumDropDepth,
                    new[] { new RailProfilePoint(0.0, defectMarker) });
                return new PointCloudMedianSectionCaptureResult(
                    string.Empty,
                    new MedianSectionExtractionResult(
                        sampleIndex,
                        new[] { new RailProfilePoint(0.0, 0.0) }),
                    maximumDropProfile);
            },
            defectGrindDepthCalculator: (angles, points) =>
            {
                defectAngleCalls.Add((points[0].Y, angles.ToArray()));
                return angles.Select(angle => new GrindDepthResult(angle, points[0].Y)).ToArray();
            });

        MeasurementGrindingWorkflowResult result = await service.RunMeasurementWorkflowAsync(
            "127.0.0.1",
            502);

        MeasurementGrindingTimesResult zeroAngleResult =
            result.Results.Single(item => item.Angle == 0);
        Assert.Equal(2, result.SampleCount);
        Assert.Equal(0.2, zeroAngleResult.RegularAverageDepth, 6);
        Assert.Equal(0.4, zeroAngleResult.DefectDepth, 6);
        Assert.Equal(0.4, zeroAngleResult.FinalGrindDepth, 6);
        Assert.Equal(8, zeroAngleResult.GrindingTimes);
        Assert.NotNull(result.MaximumDropProfile);
        Assert.Equal(PointCloudDeviceSide.Left, result.MaximumDropProfile!.Side);
        Assert.Equal(2, result.MaximumDropProfile.SampleIndex);
        Assert.Equal(3.0, result.MaximumDropProfile.MaximumDropDepth, 6);
        (double Marker, int[] Angles) leftCall = defectAngleCalls.Single(call => call.Marker == 0.4);
        (double Marker, int[] Angles) rightCall = defectAngleCalls.Single(call => call.Marker == 0.25);
        Assert.All(leftCall.Angles, angle => Assert.True(angle >= 0));
        Assert.Contains(90, leftCall.Angles);
        Assert.DoesNotContain(-35, leftCall.Angles);
        Assert.All(rightCall.Angles, angle => Assert.True(angle <= 0));
        Assert.Contains(-35, rightCall.Angles);
        Assert.DoesNotContain(90, rightCall.Angles);
    }

    [Fact]
    public async Task RunMeasurementWorkflowAsync_NoDropProfiles_CompletesWithZeroDefectDepth()
    {
        var plcClient = new FakePlcClient(
            positionCompletedValues: new[] { false, false, false, true },
            captureTriggerValues: new[] { true, false, true });
        var devices = new[]
        {
            new ConfiguredPointCloudDevice("SN-LEFT", PointCloudDeviceSide.Left),
            new ConfiguredPointCloudDevice("SN-RIGHT", PointCloudDeviceSide.Right)
        };
        int defectCalculationCount = 0;
        var service = new MeasurementParameterService(
            (ipAddress, port) => plcClient,
            configuredDeviceProvider: () => devices,
            captureSettingsProvider: () => new PointCloudCaptureSettings(1.0, 10),
            grindDepthCalculator: (angles, points) =>
                new GrindDepthCalculationResult(
                    angles.Select(angle => new GrindDepthResult(angle, 0.2)).ToArray(),
                    points),
            deviceAnalysisCapture: (device, settings, archiveContext) =>
                new PointCloudMedianSectionCaptureResult(
                    string.Empty,
                    new MedianSectionExtractionResult(
                        archiveContext!.SampleIndex,
                        new[] { new RailProfilePoint(0.0, 0.0) }),
                    null),
            defectGrindDepthCalculator: (angles, points) =>
            {
                defectCalculationCount++;
                return angles.Select(angle => new GrindDepthResult(angle, 99.0)).ToArray();
            });

        MeasurementGrindingWorkflowResult result = await service.RunMeasurementWorkflowAsync(
            "127.0.0.1",
            502);

        MeasurementGrindingTimesResult zeroAngleResult =
            result.Results.Single(item => item.Angle == 0);
        Assert.Equal(0.0, zeroAngleResult.DefectDepth, 6);
        Assert.Equal(0.2, zeroAngleResult.FinalGrindDepth, 6);
        Assert.Equal(0, defectCalculationCount);
        Assert.Null(result.MaximumDropProfile);
    }

    [Fact]
    public void MeasurementWorkflowAddresses_UseUpdatedM60M61M62Mapping()
    {
        Assert.Equal((ushort)8252, MotorParameterDefinitions.MeasurementProfileCaptureStartAddress);
        Assert.Equal((ushort)8253, MotorParameterDefinitions.MeasurementPositionCompletedAddress);
        Assert.Equal((ushort)8254, MotorParameterDefinitions.MeasurementCurrentProfileCompletedAddress);
    }

    private sealed class FakePlcClient : IPlcClient
    {
        private readonly Queue<bool> _positionCompletedValues;
        private readonly Queue<bool> _captureTriggerValues;

        public FakePlcClient()
            : this(Array.Empty<bool>(), Array.Empty<bool>())
        {
        }

        public FakePlcClient(IEnumerable<bool> positionCompletedValues, IEnumerable<bool> captureTriggerValues)
        {
            _positionCompletedValues = new Queue<bool>(positionCompletedValues);
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
            if (coilAddress == MotorParameterDefinitions.MeasurementPositionCompletedAddress)
            {
                return _positionCompletedValues.Count > 0 && _positionCompletedValues.Dequeue();
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
