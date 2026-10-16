using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Services;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class MotorViewModelSharedConnectionTests
{
    [Theory]
    [InlineData(false, "NaN")]
    [InlineData(true, "NaN")]
    [InlineData(false, "Infinity")]
    [InlineData(true, "Infinity")]
    [InlineData(false, "-Infinity")]
    [InlineData(true, "-Infinity")]
    [InlineData(false, "1e309")]
    [InlineData(true, "1e309")]
    [InlineData(false, "1e308")]
    [InlineData(true, "1e308")]
    public void ModifyCommand_NonFiniteOrOverflowInput_DoesNotWritePlc(bool int16Parameter, string input)
    {
        var plcClient = new FakePlcClient(isConnected: true);
        MotorViewModel viewModel = CreateViewModelWithImmediateContext(plcClient);
        try
        {
            string parameterName = int16Parameter
                ? MotorParameterDefinitions.WheelRotationSpeedName
                : MotorParameterDefinitions.CarJogSpeedName;
            MotorParameterItemViewModel item = viewModel.Parameters.Single(parameter => parameter.Name == parameterName);
            item.InputValue = input;

            viewModel.ModifyCommand.Execute(item);

            Assert.Empty(plcClient.Int32Writes);
            Assert.Empty(plcClient.Int16Writes);
            Assert.Equal(input, item.InputValue);
            Assert.Contains("数值", viewModel.ConnectionStatus);
            Assert.DoesNotContain("写入成功", viewModel.ConnectionStatus);
        }
        finally { viewModel.Shutdown(); }
    }

    [Theory]
    [InlineData(false, "1.25", 1250, "en-US")]
    [InlineData(false, "-1.25", -1250, "en-US")]
    [InlineData(false, "0.0025", 2, "en-US")]
    [InlineData(true, "1.2", 4, "en-US")]
    [InlineData(true, "-1.2", -4, "en-US")]
    [InlineData(true, "0.75", 2, "en-US")]
    [InlineData(false, "1,25", 1250, "fr-FR")]
    [InlineData(false, "1.25", 1250, "fr-FR")]
    public void ModifyCommand_FiniteInput_PreservesScalingAndRounding(
        bool int16Parameter, string input, int expectedRaw, string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        var plcClient = new FakePlcClient(isConnected: true);
        MotorViewModel viewModel = CreateViewModelWithImmediateContext(plcClient);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            string parameterName = int16Parameter
                ? MotorParameterDefinitions.WheelRotationSpeedName
                : MotorParameterDefinitions.CarJogSpeedName;
            MotorParameterItemViewModel item = viewModel.Parameters.Single(parameter => parameter.Name == parameterName);
            item.InputValue = input;

            viewModel.ModifyCommand.Execute(item);

            if (int16Parameter)
            {
                Assert.Equal((MotorParameterDefinitions.WheelRotationSpeedAddress, (short)expectedRaw),
                    Assert.Single(plcClient.Int16Writes));
                Assert.Empty(plcClient.Int32Writes);
            }
            else
            {
                Assert.Equal((MotorParameterDefinitions.CarJogSpeedAddress, expectedRaw),
                    Assert.Single(plcClient.Int32Writes));
                Assert.Empty(plcClient.Int16Writes);
            }
            Assert.Empty(item.InputValue);
            Assert.Contains("写入成功", viewModel.ConnectionStatus);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            viewModel.Shutdown();
        }
    }

    private static MotorViewModel CreateViewModelWithImmediateContext(IPlcClient client)
    {
        SynchronizationContext? previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            return new MotorViewModel(client, "127.0.0.1:502");
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

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

        public List<(ushort Address, int Value)> Int32Writes { get; } = new();

        public List<(ushort Address, short Value)> Int16Writes { get; } = new();

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
            Int32Writes.Add((startAddress, value));
        }

        public void WriteInt16(ushort address, short value)
        {
            Int16Writes.Add((address, value));
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
