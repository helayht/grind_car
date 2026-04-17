using System;
using System.Threading.Tasks;
using GrindCar.Definitions;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 通过 Modbus 写入测量参数。
/// </summary>
public class MeasurementParameterService : IMeasurementParameterService
{
    private const byte DefaultUnitId = 1;

    public async Task WriteMeasurementRangeAsync(string ipAddress, int port, double startPosition, double endPosition)
    {
        if (startPosition > endPosition)
        {
            throw new InvalidOperationException("测量起点位置不能大于测量终点位置。");
        }

        int startRawValue = MeasurementInputParser.ToScaledInt32(
            startPosition,
            MotorParameterDefinitions.MeasurementStartPositionScale,
            MotorParameterDefinitions.MeasurementStartPositionName);
        int endRawValue = MeasurementInputParser.ToScaledInt32(
            endPosition,
            MotorParameterDefinitions.MeasurementEndPositionScale,
            MotorParameterDefinitions.MeasurementEndPositionName);

        using IPlcClient plcClient = new PlcModbusCommunicator(ipAddress, port, DefaultUnitId);
        await plcClient.ConnectAsync().ConfigureAwait(false);
        plcClient.WriteInt32(MotorParameterDefinitions.MeasurementStartPositionAddress, startRawValue);
        plcClient.WriteInt32(MotorParameterDefinitions.MeasurementEndPositionAddress, endRawValue);
        plcClient.Disconnect();
    }

    public async Task StartMeasurementMotionAsync(string ipAddress, int port)
    {
        using IPlcClient plcClient = new PlcModbusCommunicator(ipAddress, port, DefaultUnitId);
        await plcClient.ConnectAsync().ConfigureAwait(false);
        await plcClient.WriteSingleCoilAsync(MotorParameterDefinitions.MeasurementMotionStartAddress, true)
            .ConfigureAwait(false);
        plcClient.Disconnect();
    }
}