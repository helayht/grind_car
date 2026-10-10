using System;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.Measurement;

namespace GrindCar.Services.Measurement;

public partial class MeasurementParameterService
{
    public Task WriteMeasurementParametersAsync(string ipAddress, int port, MeasurementParameters parameters)
    {
        if (parameters?.Positions == null) throw new InvalidOperationException("测量参数不完整。");
        parameters.Positions.Validate();
        if (!double.IsFinite(parameters.SpeedMetersPerMinute) || parameters.SpeedMetersPerMinute <= 0)
            throw new InvalidOperationException("小车测量行走速度必须是大于0的有限数值。");
        MeasurementPositions positions = parameters.Positions;
        var writes = new (string Name, ushort Address, int Value)[]
        {
            (MotorParameterDefinitions.MeasurementStartPositionName, MotorParameterDefinitions.MeasurementStartPositionAddress,
                MeasurementInputParser.ToScaledInt32(positions.Start, MotorParameterDefinitions.MeasurementStartPositionScale, MotorParameterDefinitions.MeasurementStartPositionName)),
            (MotorParameterDefinitions.MeasurementEndPositionName, MotorParameterDefinitions.MeasurementEndPositionAddress,
                MeasurementInputParser.ToScaledInt32(positions.End, MotorParameterDefinitions.MeasurementEndPositionScale, MotorParameterDefinitions.MeasurementEndPositionName)),
            (MotorParameterDefinitions.ProfilerMeasurementPositionName, MotorParameterDefinitions.ProfilerMeasurementPositionAddress,
                MeasurementInputParser.ToScaledInt32(positions.ProfilerPosition, MotorParameterDefinitions.ProfilerMeasurementPositionScale, MotorParameterDefinitions.ProfilerMeasurementPositionName)),
            (MotorParameterDefinitions.ProfilerAvoidancePositionName, MotorParameterDefinitions.ProfilerAvoidancePositionAddress,
                MeasurementInputParser.ToScaledInt32(positions.AvoidancePosition, MotorParameterDefinitions.ProfilerAvoidancePositionScale, MotorParameterDefinitions.ProfilerAvoidancePositionName)),
            (MotorParameterDefinitions.CarMeasurementSpeedName, MotorParameterDefinitions.CarMeasurementSpeedAddress,
                MeasurementInputParser.ToScaledInt32(parameters.SpeedMetersPerMinute, MotorParameterDefinitions.CarMeasurementSpeedScale, MotorParameterDefinitions.CarMeasurementSpeedName))
        };
        return Task.Run(async () =>
        {
            using IPlcClient client = _plcClientFactory(ipAddress, port);
            await client.ConnectAsync().ConfigureAwait(false);
            int completed = 0;
            foreach (var write in writes)
            {
                try
                {
                    if (client.ReadSingleCoil(MotorParameterDefinitions.MeasurementRunningAddress))
                        throw new InvalidOperationException("测量运行中，不能写入测量参数。");
                    client.WriteInt32(write.Address, write.Value);
                    completed++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"{write.Name}（D{write.Address}）写入失败；已完成{completed}/{writes.Length}项，PLC可能已部分写入。{ex.Message}", ex);
                }
            }
        });
    }
}
