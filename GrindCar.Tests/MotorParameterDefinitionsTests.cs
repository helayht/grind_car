using System.Collections.Generic;
using System.Linq;
using GrindCar.Definitions;
using GrindCar.Services.Motor;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class MotorParameterDefinitionsTests
{
    [Fact]
    public void MeasurementGrindingTimesAddresses_UseInt32RegisterStride()
    {
        IReadOnlyList<int> angles = MotorParameterDefinitions.MeasurementGrindingAngles;

        Assert.Equal(1800, MotorParameterDefinitions.MeasurementGrindingTimesAddresses[angles[0]]);
        Assert.Equal(1802, MotorParameterDefinitions.MeasurementGrindingTimesAddresses[angles[1]]);
        Assert.Equal(1804, MotorParameterDefinitions.MeasurementGrindingTimesAddresses[angles[2]]);
    }

    [Fact]
    public void MeasurementGrindingTimesAddresses_DoNotOverlapInt32Registers()
    {
        var occupiedRegisters = new HashSet<ushort>();

        foreach (int angle in MotorParameterDefinitions.MeasurementGrindingAngles)
        {
            ushort startAddress = MotorParameterDefinitions.MeasurementGrindingTimesAddresses[angle];

            Assert.True(occupiedRegisters.Add(startAddress));
            Assert.True(occupiedRegisters.Add((ushort)(startAddress + 1)));
        }

        Assert.Equal(
            MotorParameterDefinitions.MeasurementGrindingAngles.Count * 2,
            occupiedRegisters.Count);
        Assert.Equal(
            MotorParameterDefinitions.MeasurementGrindingAngles.Count,
            MotorParameterDefinitions.MeasurementGrindingTimesAddresses.Values.Distinct().Count());
    }

    [Fact]
    public void WheelRunExecution_UsesM710BoolCoilAddress()
    {
        IReadOnlyDictionary<string, MotorParameterWriteSpec> specs = MotorParameterSpecProvider.BuildWriteSpecs();

        Assert.Contains(MotorParameterDefinitions.WheelRunExecutionName, MotorParameterDefinitions.WritableParameterNames);
        MotorParameterWriteSpec spec = specs[MotorParameterDefinitions.WheelRunExecutionName];
        Assert.Equal((ushort)(710 + MotorParameterDefinitions.MAddressOffset), spec.Address);
        Assert.Equal(MotorParameterDataKind.Bool, spec.Kind);
    }

    [Fact]
    public void WheelRunCommand_UsesD1152Int16Address()
    {
        IReadOnlyDictionary<string, MotorParameterWriteSpec> specs = MotorParameterSpecProvider.BuildWriteSpecs();

        Assert.Contains(MotorParameterDefinitions.WheelRunCommandName, MotorParameterDefinitions.WritableParameterNames);
        MotorParameterWriteSpec spec = specs[MotorParameterDefinitions.WheelRunCommandName];
        Assert.Equal((ushort)1152, spec.Address);
        Assert.Equal(MotorParameterDataKind.Int16, spec.Kind);
    }

    [Fact]
    public void MotorViewModel_WheelRunCommand_ProvidesFixedCommandOptions()
    {
        var viewModel = new MotorViewModel();
        MotorParameterItemViewModel item = Assert.Single(
            viewModel.Parameters.Where(parameter => parameter.Name == MotorParameterDefinitions.WheelRunCommandName));

        Assert.Equal(
            new[]
            {
                ("旋转", (short)1),
                ("自由停机", (short)5),
                ("减速停机", (short)6),
                ("故障复位", (short)7)
            },
            item.CommandOptions.Select(option => (option.DisplayName, option.Value)).ToArray());

        viewModel.Shutdown();
    }
}
