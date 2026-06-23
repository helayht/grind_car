using System.Threading.Tasks;
using GrindCar.Services;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class MotorViewModelSharedConnectionTests
{
    [Fact]
    public async Task StartSharedPollingAsync_WhenSharedClientConnected_StartsPolling()
    {
        var plcClient = new FakePlcClient(isConnected: true);
        var viewModel = new MotorViewModel(plcClient, "127.0.0.1:502");

        bool started = await viewModel.StartSharedPollingAsync();
        viewModel.Shutdown();

        Assert.True(started);
        Assert.False(plcClient.DisconnectCalled);
    }

    [Fact]
    public async Task StartSharedPollingAsync_WhenSharedClientDisconnected_DoesNotStartPolling()
    {
        var plcClient = new FakePlcClient(isConnected: false);
        var viewModel = new MotorViewModel(plcClient, "127.0.0.1:502");

        bool started = await viewModel.StartSharedPollingAsync();
        viewModel.Shutdown();

        Assert.False(started);
        Assert.False(plcClient.DisconnectCalled);
    }

    private sealed class FakePlcClient : IPlcClient
    {
        public FakePlcClient(bool isConnected)
        {
            IsConnected = isConnected;
        }

        public bool IsConnected { get; }

        public bool DisconnectCalled { get; private set; }

        public Task ConnectAsync()
        {
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            DisconnectCalled = true;
        }

        public int ReadInt32(ushort startAddress)
        {
            return 0;
        }

        public bool ReadSingleCoil(ushort coilAddress)
        {
            return false;
        }

        public void WriteInt32(ushort startAddress, int value)
        {
        }

        public void WriteInt16(ushort address, short value)
        {
        }

        public Task WriteSingleCoilAsync(ushort coilAddress, bool value)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
