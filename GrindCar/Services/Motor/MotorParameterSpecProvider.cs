using System.Collections.Generic;
using GrindCar.Definitions;

namespace GrindCar.Services.Motor;

/// <summary>
/// 提供电机参数读写映射元数据。
/// </summary>
public static class MotorParameterSpecProvider
{
    public static IReadOnlyDictionary<string, double> BuildReadScales()
    {
        return new Dictionary<string, double>
        {
            [MotorParameterDefinitions.CarCurrentPositionName] = MotorParameterDefinitions.CarCurrentPositionScale,
            [MotorParameterDefinitions.WheelLongitudinalCurrentPositionName] = MotorParameterDefinitions.WheelLongitudinalCurrentPositionScale,
            [MotorParameterDefinitions.WheelLateralCurrentPositionName] = MotorParameterDefinitions.WheelLateralCurrentPositionScale,
            [MotorParameterDefinitions.WheelAngleCurrentPositionName] = MotorParameterDefinitions.WheelAngleCurrentPositionScale,
            [MotorParameterDefinitions.ProfilerCurrentPositionName] = MotorParameterDefinitions.ProfilerCurrentPositionScale
        };
    }

    public static IReadOnlyDictionary<string, MotorParameterWriteSpec> BuildWriteSpecs()
    {
        return new Dictionary<string, MotorParameterWriteSpec>
        {
            [MotorParameterDefinitions.CarJogSpeedName] = new(
                MotorParameterDefinitions.CarJogSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.CarJogSpeedScale),
            [MotorParameterDefinitions.CarPositionTargetAddressName] = new(
                MotorParameterDefinitions.CarPositionTargetAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.CarPositionTargetAddressScale),
            [MotorParameterDefinitions.CarPositionSpeedName] = new(
                MotorParameterDefinitions.CarPositionSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.CarPositionSpeedScale),
            [MotorParameterDefinitions.CarPositionStartName] = new(
                MotorParameterDefinitions.CarPositionStartAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.CarJogForwardName] = new(
                MotorParameterDefinitions.CarJogForwardAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.CarJogBackwardName] = new(
                MotorParameterDefinitions.CarJogBackwardAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.CarHomeName] = new(
                MotorParameterDefinitions.CarHomeAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.CarFaultResetName] = new(
                MotorParameterDefinitions.CarFaultResetAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),

            [MotorParameterDefinitions.WheelLongitudinalJogSpeedName] = new(
                MotorParameterDefinitions.WheelLongitudinalJogSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelLongitudinalJogSpeedScale),
            [MotorParameterDefinitions.WheelLongitudinalPositionTargetAddressName] = new(
                MotorParameterDefinitions.WheelLongitudinalPositionTargetAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelLongitudinalPositionTargetAddressScale),
            [MotorParameterDefinitions.WheelLongitudinalPositionSpeedName] = new(
                MotorParameterDefinitions.WheelLongitudinalPositionSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelLongitudinalPositionSpeedScale),
            [MotorParameterDefinitions.WheelLongitudinalPositionStartName] = new(
                MotorParameterDefinitions.WheelLongitudinalPositionStartAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLongitudinalJogDownName] = new(
                MotorParameterDefinitions.WheelLongitudinalJogDownAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLongitudinalJogUpName] = new(
                MotorParameterDefinitions.WheelLongitudinalJogUpAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLongitudinalHomeName] = new(
                MotorParameterDefinitions.WheelLongitudinalHomeAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLongitudinalFaultResetName] = new(
                MotorParameterDefinitions.WheelLongitudinalFaultResetAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),

            [MotorParameterDefinitions.WheelLateralJogSpeedName] = new(
                MotorParameterDefinitions.WheelLateralJogSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelLateralJogSpeedScale),
            [MotorParameterDefinitions.WheelLateralPositionTargetAddressName] = new(
                MotorParameterDefinitions.WheelLateralPositionTargetAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelLateralPositionTargetAddressScale),
            [MotorParameterDefinitions.WheelLateralPositionSpeedName] = new(
                MotorParameterDefinitions.WheelLateralPositionSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelLateralPositionSpeedScale),
            [MotorParameterDefinitions.WheelLateralPositionStartName] = new(
                MotorParameterDefinitions.WheelLateralPositionStartAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLateralJogLeftName] = new(
                MotorParameterDefinitions.WheelLateralJogLeftAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLateralJogRightName] = new(
                MotorParameterDefinitions.WheelLateralJogRightAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLateralHomeName] = new(
                MotorParameterDefinitions.WheelLateralHomeAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelLateralFaultResetName] = new(
                MotorParameterDefinitions.WheelLateralFaultResetAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),

            [MotorParameterDefinitions.WheelAngleJogSpeedName] = new(
                MotorParameterDefinitions.WheelAngleJogSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelAngleJogSpeedScale),
            [MotorParameterDefinitions.WheelAnglePositionTargetAddressName] = new(
                MotorParameterDefinitions.WheelAnglePositionTargetAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelAnglePositionTargetAddressScale),
            [MotorParameterDefinitions.WheelAnglePositionSpeedName] = new(
                MotorParameterDefinitions.WheelAnglePositionSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.WheelAnglePositionSpeedScale),
            [MotorParameterDefinitions.WheelAnglePositionStartName] = new(
                MotorParameterDefinitions.WheelAnglePositionStartAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelAngleJogReverseName] = new(
                MotorParameterDefinitions.WheelAngleJogReverseAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelAngleJogForwardName] = new(
                MotorParameterDefinitions.WheelAngleJogForwardAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelAngleHomeName] = new(
                MotorParameterDefinitions.WheelAngleHomeAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.WheelAngleFaultResetName] = new(
                MotorParameterDefinitions.WheelAngleFaultResetAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),

            [MotorParameterDefinitions.ProfilerJogSpeedName] = new(
                MotorParameterDefinitions.ProfilerJogSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.ProfilerJogSpeedScale),
            [MotorParameterDefinitions.ProfilerPositionTargetAddressName] = new(
                MotorParameterDefinitions.ProfilerPositionTargetAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.ProfilerPositionTargetAddressScale),
            [MotorParameterDefinitions.ProfilerPositionSpeedName] = new(
                MotorParameterDefinitions.ProfilerPositionSpeedAddress,
                MotorParameterDataKind.Int32,
                MotorParameterDefinitions.ProfilerPositionSpeedScale),
            [MotorParameterDefinitions.ProfilerPositionStartName] = new(
                MotorParameterDefinitions.ProfilerPositionStartAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.ProfilerJogDownName] = new(
                MotorParameterDefinitions.ProfilerJogDownAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.ProfilerJogUpName] = new(
                MotorParameterDefinitions.ProfilerJogUpAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.ProfilerHomeName] = new(
                MotorParameterDefinitions.ProfilerHomeAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),
            [MotorParameterDefinitions.ProfilerFaultResetName] = new(
                MotorParameterDefinitions.ProfilerFaultResetAddress,
                MotorParameterDataKind.Bool,
                MotorParameterDefinitions.BoolScale),

            [MotorParameterDefinitions.WheelRotationSpeedName] = new(
                MotorParameterDefinitions.WheelRotationSpeedAddress,
                MotorParameterDataKind.Int16,
                MotorParameterDefinitions.WheelRotationSpeedScale),
            [MotorParameterDefinitions.WheelQualityControlName] = new(
                MotorParameterDefinitions.WheelQualityControlAddress,
                MotorParameterDataKind.Int16,
                MotorParameterDefinitions.WheelQualityControlScale)
        };
    }
}