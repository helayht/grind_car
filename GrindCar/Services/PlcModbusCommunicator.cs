using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Modbus.Device;

namespace GrindCar.Services;

/// <summary>
/// PLC Modbus 通信封装（连接、读写、轮询）。
/// </summary>
public partial class PlcModbusCommunicator : IPlcClient
{
    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private readonly string _plcIpAddress;
    private readonly int _plcPort;
    private readonly byte _unitId;
    private bool _isConnected;

    private volatile bool _isCompleted1;
    private volatile bool _isCompleted2;
    public List<object> LeftValue { get; } = new();
    public List<object> RightValue { get; } = new();

    // --- 持续读取 D 寄存器 (Float) 的相关成员 ---
    private CancellationTokenSource? _continuousReadDRegistersCts;
    private Task? _continuousReadDRegistersTask;
    public event EventHandler<(float D1054Value, float D1154Value)>? OnDRegisterFloatReadingsReceived;
    private ushort _d1054ModbusAddress;
    private ushort _d1154ModbusAddress;
    private int _dRegisterPollIntervalMs;

    // --- 持续读取 Coil (Bool) 的相关成员 - 任务 1 ---
    private CancellationTokenSource? _continuousReadCoilsCts;
    private Task? _continuousReadCoilsTask;
    public event EventHandler<(ushort startAddress, bool[] values)>? OnCoilReadingsReceived;
    private ushort _coilStartAddress;
    private ushort _numberOfCoilsToRead;
    private int _coilPollIntervalMs;

    // --- 持续读取 Coil (Bool) 的相关成员 - 任务 2 ---
    private CancellationTokenSource? _continuousReadCoilsCts2;
    private Task? _continuousReadCoilsTask2;
    public event EventHandler<(ushort startAddress, bool[] values)>? OnCoilReadingsReceived2;
    private ushort _coilStartAddress2;
    private ushort _numberOfCoilsToRead2;
    private int _coilPollIntervalMs2;

    private const int ConnectTimeoutMs = 5000;
    private const int TransportReadTimeoutMs = 1500;
    private const int TransportWriteTimeoutMs = 1500;
    private const int DefaultCoilPollIntervalMs = 100;
    private const float DefaultCommonSpeed = 20f;

    private static readonly ushort[] LeftFloatAddresses =
    {
        1000, 1002, 1004, 1006, 1050, 1052, 1054
    };

    private static readonly ushort[] LeftCoilAddresses =
    {
        9192, 9193, 9194, 9195, 9196, 9196,
        9242, 9243, 9244, 9245, 9246,
        9252, 9253,
        9392, 9393
    };

    private static readonly ushort[] RightFloatAddresses =
    {
        1100, 1102, 1104, 1106, 1150, 1152, 1154
    };

    private static readonly ushort[] RightCoilAddresses =
    {
        9292, 9293, 9294, 9295, 9296, 9296,
        9342, 9343, 9344, 9345, 9346,
        9352, 9353
    };

    private const ushort LeftTailFloatAddress = 1200;

    private const ushort MoveXPositionAddress = 1104;
    private const ushort MoveXSpeedAddress = 1106;
    private const ushort MoveXRunFlagAddress = 9295;

    private const ushort MoveYPositionAddress = 1004;
    private const ushort MoveYSpeedAddress = 1006;
    private const ushort MoveYRunFlagAddress = 9195;

    private const ushort MoveXCompletionFlagAddress = 9353;
    private const ushort MoveYCompletionFlagAddress = 9253;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="ipAddress">PLC 的 IP 地址。</param>
    /// <param name="port">PLC 的通信端口。</param>
    /// <param name="unitId">Modbus 单元标识。</param>
    public PlcModbusCommunicator(string ipAddress, int port, byte unitId)
    {
        _plcIpAddress = ipAddress;
        _plcPort = port;
        _unitId = unitId;
    }
}
