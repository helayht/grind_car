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
#if NETSTANDARD2_1_OR_GREATER || NET
            await _tcpClient.ConnectAsync(_plcIpAddress, _plcPort).ConfigureAwait(false);
#else
            // 为旧版 .NET Framework 提供兼容
            var connectTask = _tcpClient.ConnectAsync(_plcIpAddress, _plcPort);
            if (await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs)).ConfigureAwait(false) != connectTask)
            {
                _tcpClient?.Dispose();
                _tcpClient = null;
                throw new TimeoutException("PLC connection timed out.");
            }

            await connectTask.ConfigureAwait(false);
#endif

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
    /// 以同步方式建立与 PLC 的 Modbus TCP 连接。
    /// </summary>
    public void Connect()
    {
        if (_isConnected)
        {
            Debug.WriteLine("Modbus: 已经连接，无需重复连接。");
            return;
        }

        try
        {
            _tcpClient = new TcpClient();

            IAsyncResult result = _tcpClient.BeginConnect(_plcIpAddress, _plcPort, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(ConnectTimeoutMs), true);

            if (success && _tcpClient.Connected)
            {
                _tcpClient.EndConnect(result);
            }
            else
            {
                _tcpClient.Close();
                _tcpClient.Dispose();
                _tcpClient = null;
                throw new TimeoutException($"连接到 PLC {_plcIpAddress}:{_plcPort} 超时 ({ConnectTimeoutMs}ms)。");
            }

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
            throw new Exception($"无法连接到 PLC (Socket Error): {ex.Message}", ex);
        }
        catch (TimeoutException)
        {
            _isConnected = false;
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

        StopContinuousDRegisterReading();
        StopContinuousCoilReading();
        StopContinuousCoilReading2();

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
    /// 释放连接、后台任务和相关资源。
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
            StopContinuousDRegisterReading();
            StopContinuousCoilReading();
            StopContinuousCoilReading2();

            Disconnect();
        }
    }
}
