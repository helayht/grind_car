using System;
using GrindCar.Definitions;
using GrindCar.Services.Measurement;

namespace GrindCar.Models.Measurement;

public sealed record MeasurementPositions(double Start, double End, double ProfilerPosition, double AvoidancePosition)
{
    public void Validate()
    {
        MeasurementInputParser.ToScaledInt32(Start, MotorParameterDefinitions.MeasurementStartPositionScale, MotorParameterDefinitions.MeasurementStartPositionName);
        MeasurementInputParser.ToScaledInt32(End, MotorParameterDefinitions.MeasurementEndPositionScale, MotorParameterDefinitions.MeasurementEndPositionName);
        MeasurementInputParser.ToScaledInt32(ProfilerPosition, MotorParameterDefinitions.ProfilerMeasurementPositionScale, MotorParameterDefinitions.ProfilerMeasurementPositionName);
        MeasurementInputParser.ToScaledInt32(AvoidancePosition, MotorParameterDefinitions.ProfilerAvoidancePositionScale, MotorParameterDefinitions.ProfilerAvoidancePositionName);
        if (Start > End) throw new InvalidOperationException("测量起点不能大于终点。");
    }
}

public sealed record MeasurementParameters(MeasurementPositions Positions, double SpeedMetersPerMinute);
