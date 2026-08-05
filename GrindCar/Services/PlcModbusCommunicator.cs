using System.Net.Sockets;
using Modbus.Device;

namespace GrindCar.Services;

/// <summary>
/// PLC Modbus 通信封装（连接与读写）。
/// </summary>
public partial class PlcModbusCommunicator : IPlcClient
{
    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private readonly string _plcIpAddress;
    private readonly int _plcPort;
    private readonly byte _unitId;
    private bool _isConnected;

    private const int TransportReadTimeoutMs = 1500;
    private const int TransportWriteTimeoutMs = 1500;

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
