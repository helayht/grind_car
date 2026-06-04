using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GrindCar.Definitions;
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

    private sealed class FakePlcClient : IPlcClient
    {
        public bool IsConnected => ConnectCalled;

        public bool ConnectCalled { get; private set; }

        public bool DisconnectCalled { get; private set; }

        public Dictionary<ushort, int> WrittenInt32Values { get; } = new();

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
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }
}
