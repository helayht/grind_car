using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using Modbus.Device;

namespace GrindCar.Services;

public partial class PlcModbusCommunicator
{
    /// <summary>
    /// 建立与 PLC 的 Modbus TCP 连接 (异步)。
    /// </summary>
    /// <returns>表示异步连接过程的任务。</returns>
    public async Task ConnectAsync()
    {
        if (_isConnected)
        {
            Debug.WriteLine("Modbus: 已经连接，无需重复连接。");
            return;
        }

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_plcIpAddress, _plcPort).ConfigureAwait(false);

            _modbusMaster = ModbusIpMaster.CreateIp(_tcpClient);
            _modbusMaster.Transport.ReadTimeout = TransportReadTimeoutMs;
            _modbusMaster.Transport.WriteTimeout = TransportWriteTimeoutMs;

            _isConnected = true;
            Debug.WriteLine($"Modbus: 成功连接到 PLC {_plcIpAddress}:{_plcPort} (Unit ID: {_unitId})");
        }
        catch (SocketException ex)
        {
            _isConnected = false;
            Debug.WriteLine($"Modbus: Socket 错误 ({ex.SocketErrorCode}) 连接到 PLC: {ex.Message}");
            SafeDisposeTcpClientAndMaster();
            throw new Exception($"无法连接到 PLC: {ex.Message}", ex);
        }
        catch (TimeoutException ex)
        {
            _isConnected = false;
            Debug.WriteLine($"Modbus: 连接 PLC 超时: {ex.Message}");
            SafeDisposeTcpClientAndMaster();
            throw;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            Debug.WriteLine($"Modbus: 连接到 PLC 时发生错误: {ex.Message}");
            SafeDisposeTcpClientAndMaster();
            throw new Exception($"无法连接到 PLC: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 断开与 PLC 的连接并释放资源。
    /// </summary>
    public void Disconnect()
    {
        if (!_isConnected && _modbusMaster == null && _tcpClient == null)
        {
            Debug.WriteLine("Modbus: 已经断开连接，无需重复操作。");
            return;
        }

        SafeDisposeTcpClientAndMaster();
        _isConnected = false;
        Debug.WriteLine("Modbus: 已断开与 PLC 的连接。");
    }

    public bool IsConnected => _isConnected && _tcpClient != null && _tcpClient.Connected;

    /// <summary>
    /// 安全释放 TcpClient 和 Modbus 主站对象。
    /// </summary>
    private void SafeDisposeTcpClientAndMaster()
    {
        _modbusMaster?.Dispose();
        _modbusMaster = null;
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    /// <summary>
    /// 释放连接及相关资源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 执行资源释放逻辑。
    /// </summary>
    /// <param name="disposing">为 <c>true</c> 时释放托管资源。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disconnect();
        }
    }
}
