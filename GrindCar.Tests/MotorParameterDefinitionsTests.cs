using System.Collections.Generic;
using System.Linq;
using GrindCar.Definitions;
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
}
