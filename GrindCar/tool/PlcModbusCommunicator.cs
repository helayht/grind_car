using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Modbus.Device;
// 引入 NModbus 库

// 用于 Debug.WriteLine

namespace GrindCar.tool;

public class PlcModbusCommunicator : IDisposable
{
    private TcpClient _tcpClient;
    private IModbusMaster _modbusMaster;
    private readonly string _plcIpAddress;
    private readonly int _plcPort;
    private readonly byte _unitId;
    private bool _isConnected = false;
    public bool _isCompleted1 = false;
    public bool _isCompleted2 = false;
    public List<object> LeftValue = new List<object>();
    public List<object> RightValue = new List<object>();

    // --- 持续读取 D 寄存器 (Float) 的相关成员 ---
    private CancellationTokenSource _continuousReadDRegistersCts;
    private Task _continuousReadDRegistersTask;
    public event EventHandler<(float D1054Value, float D1154Value)> OnDRegisterFloatReadingsReceived;
    private ushort _d1054ModbusAddress;
    private ushort _d1154ModbusAddress;
    private int _dRegisterPollIntervalMs;

    // --- 持续读取 Coil (Bool) 的相关成员 - 任务 1 ---
    private CancellationTokenSource _continuousReadCoilsCts;
    private Task _continuousReadCoilsTask;
    public event EventHandler<(ushort startAddress, bool[] values)> OnCoilReadingsReceived; // 事件给任务1
    private ushort _coilStartAddress;
    private ushort _numberOfCoilsToRead;
    private int _coilPollIntervalMs;

    // --- 持续读取 Coil (Bool) 的相关成员 - 任务 2 ---
    private CancellationTokenSource _continuousReadCoilsCts2; // 为任务2准备的Cts
    private Task _continuousReadCoilsTask2;                  // 为任务2准备的Task
    public event EventHandler<(ushort startAddress, bool[] values)> OnCoilReadingsReceived2; // 事件给任务2
    private ushort _coilStartAddress2;                       // 任务2的起始地址
    private ushort _numberOfCoilsToRead2;                    // 任务2读取的数量
    private int _coilPollIntervalMs2;                       // 任务2的轮询间隔


    /// <summary>
    /// 构造函数
    /// </summary>
    public PlcModbusCommunicator(string ipAddress, int port, byte unitId)
    {
        _plcIpAddress = ipAddress;
        _plcPort = port;
        _unitId = unitId;
    }

    /// <summary>
    /// 建立与 PLC 的 Modbus TCP 连接 (异步)
    /// </summary>
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
            var connectTask = _tcpClient.ConnectAsync(_plcIpAddress, _plcPort); // ConnectAsync 在旧版也可用，但可能表现不同
            if (await Task.WhenAny(connectTask, Task.Delay(5000)).ConfigureAwait(false) != connectTask) // 5秒连接超时
            {
                 _tcpClient?.Dispose(); // 超时则释放
                 _tcpClient = null;
                 throw new TimeoutException("PLC connection timed out.");
            }
            // 如果没有超时，ConnectAsync 应该已经完成或抛出异常
            await connectTask.ConfigureAwait(false); // 确保捕获连接异常
#endif

            _modbusMaster = ModbusIpMaster.CreateIp(_tcpClient);
            _modbusMaster.Transport.ReadTimeout = 1500;
            _modbusMaster.Transport.WriteTimeout = 1500;

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
        catch (TimeoutException ex) // 捕获上面添加的超时异常
        {
            _isConnected = false;
            Debug.WriteLine($"Modbus: 连接 PLC 超时: {ex.Message}");
            SafeDisposeTcpClientAndMaster();
            throw; // 重新抛出超时异常
        }
        catch (Exception ex)
        {
            _isConnected = false;
            Debug.WriteLine($"Modbus: 连接到 PLC 时发生错误: {ex.Message}");
            SafeDisposeTcpClientAndMaster();
            throw new Exception($"无法连接到 PLC: {ex.Message}", ex);
        }
    }

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

            // --- 同步连接并处理超时 ---
            // TcpClient.Connect 方法本身是阻塞的，但没有内置的简单超时参数。
            // 我们可以使用 IAsyncResult 和 WaitOne 来模拟超时，或者更简单地依赖于操作系统本身的超时（可能较长）。
            // 为了更可控的超时，我们通常会使用异步API配合超时，但既然要求同步，可以这样做：

            IAsyncResult result = _tcpClient.BeginConnect(_plcIpAddress, _plcPort, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(5000), true);

            if (success && _tcpClient.Connected)
            {
                _tcpClient.EndConnect(result); // 完成连接
            }
            else
            {
                // 连接未在超时时间内成功，或者 WaitOne 返回 false
                _tcpClient.Close(); // 关闭尝试中的 TcpClient
                _tcpClient.Dispose();
                _tcpClient = null;
                throw new TimeoutException($"连接到 PLC {_plcIpAddress}:{_plcPort} 超时 ({5000}ms)。");
            }
            // --- 连接成功后的操作 ---
            _modbusMaster = ModbusIpMaster.CreateIp(_tcpClient);
            _modbusMaster.Transport.ReadTimeout = 1500; // 仍然可以设置读写超时
            _modbusMaster.Transport.WriteTimeout = 1500;

            _isConnected = true;
            Debug.WriteLine($"Modbus: 成功连接到 PLC {_plcIpAddress}:{_plcPort} (Unit ID: {_unitId})");
        }
        catch (SocketException ex)
        {
            _isConnected = false;
            Debug.WriteLine($"Modbus: Socket 错误 ({ex.SocketErrorCode}) 连接到 PLC: {ex.Message}");
            SafeDisposeTcpClientAndMaster(); // 确保资源被释放
            throw new Exception($"无法连接到 PLC (Socket Error): {ex.Message}", ex);
        }
        catch (TimeoutException) // 重新抛出上面我们自己抛出的 TimeoutException
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

  


    private void SafeDisposeTcpClientAndMaster()
    {
        _modbusMaster?.Dispose();
        _modbusMaster = null;
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    /// <summary>
    /// 断开与 PLC 的连接并释放资源
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
        StopContinuousCoilReading2(); // 停止任务2

        SafeDisposeTcpClientAndMaster();
        _isConnected = false;
        Debug.WriteLine("Modbus: 已断开与 PLC 的连接。");
    }

    public bool IsConnected => _isConnected && _tcpClient != null && _tcpClient.Connected;

    // --- 单次读写方法 (保持不变，为了简洁省略，请参考你之前的代码) ---
    // WriteSingleCoilAsync, ReadCoilAsync, WriteSingleRegisterAsync, ReadHoldingRegisterAsync
    // WriteFloatAsync, ReadFloat, WriteFloat (sync), ReadInt32Async
    // ... (这里假设这些方法已存在且 WriteSingleCoilAsync 返回 Task)
    public void WriteSingleCoil(ushort coilAddress, bool value) 
    { 
        EnsureConnected(); 
        try 
        { 
            _modbusMaster.WriteSingleCoil(1, coilAddress, value); 
            Debug.WriteLine($"Modbus: Wrote Coil {coilAddress}={value}"); 
        } catch (Exception ex) 
        {
            Debug.WriteLine($"Err Wr Coil {coilAddress}:{ex.Message}"); 
            HandleModbusError(ex); throw; 
        }
    }
    public bool ReadSingleCoil(ushort coilAddress) 
    { 
        EnsureConnected(); 
        try 
        { 
            bool[] r = _modbusMaster.ReadCoils(1, coilAddress, 1); 
            if (r != null && r.Length > 0) 
                return r[0]; 
            throw new Modbus.SlaveException(); 
        } catch (Exception ex) 
        { 
            Debug.WriteLine($"Err Rd Coil {coilAddress}:{ex.Message}"); 
            HandleModbusError(ex); throw; 
        } 
    }



    /// <summary>
    /// 写入单个 Modbus Coil (异步, 返回 Task)
    /// </summary>
    public async Task WriteSingleCoilAsync(ushort coilAddress, bool value)
    {
        EnsureConnected();
        try
        {
            await _modbusMaster.WriteSingleCoilAsync(_unitId, coilAddress, value).ConfigureAwait(false);
            Debug.WriteLine($"Modbus: 成功写入布尔值 {value} 到 Coil 地址 {coilAddress}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 写入 Coil 地址 {coilAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }
    
    public void WriteFloat(ushort startAddress, float value)
    {
        EnsureConnected();
        try
        {
            ushort[] dataToWrite = FloatToUshorts(value);
            _modbusMaster.WriteMultipleRegisters(_unitId, startAddress, dataToWrite);
            Debug.WriteLine($"Modbus: 成功写入浮点数 {value} 到 Holding Register 地址 {startAddress}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 写入 Float 到 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }
    public float ReadFloat(ushort startAddress)
    {
        EnsureConnected();
        try
        {
            ushort[] registers = _modbusMaster.ReadHoldingRegisters(_unitId, startAddress, 2);
            if (registers != null && registers.Length == 2)
            {
                float value = UshortsToFloat(registers);
                Debug.WriteLine($"Modbus: 成功读取浮点数 {value} 从 Holding Register 地址 {startAddress}");
                return value;
            }
            else
            {
                Debug.WriteLine($"Modbus: 从 Holding Register 地址 {startAddress} 读取 Float 失败，未返回足够数据。");
                throw new Exception($"Failed to read 2 registers for Float from address {startAddress}.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 读取 Float 从 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw ex;
        }
    }

    public int ReadInt32(ushort startAddress)
    {
        EnsureConnected();
        try
        {
            ushort[] registers = _modbusMaster.ReadHoldingRegisters(_unitId, startAddress, 2);
            if (registers != null && registers.Length == 2)
            {
                int value = UshortsToInt32(registers);
                Debug.WriteLine($"Modbus: 成功读取整型 {value} 从 Holding Register 地址 {startAddress}");
                return value;
            }
            else
            {
                Debug.WriteLine($"Modbus: 从 Holding Register 地址 {startAddress} 读取 Int32 失败，未返回足够数据。");
                throw new Exception($"Failed to read 2 registers for Int32 from address {startAddress}.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 读取 Int32 从 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }


    // --- 持续读取 D 寄存器 (Float) 的功能  ---
    public void StartContinuousDRegisterReading(ushort d1054ModbusAddress, ushort d1154ModbusAddress, int pollIntervalMs)
    {
        if (_continuousReadDRegistersTask != null && !_continuousReadDRegistersTask.IsCompleted) { Debug.WriteLine("Modbus: 持续读取 D 寄存器任务已经在运行。"); return; }
        EnsureConnected();
        _d1054ModbusAddress = d1054ModbusAddress;
        _d1154ModbusAddress = d1154ModbusAddress;
        _dRegisterPollIntervalMs = pollIntervalMs;
        _continuousReadDRegistersCts = new CancellationTokenSource();
        _continuousReadDRegistersTask = Task.Run(async () => await MonitorDRegistersLoopAsync(_continuousReadDRegistersCts.Token), _continuousReadDRegistersCts.Token);
        Debug.WriteLine($"Modbus: 开始持续读取 D 寄存器 (Modbus 地址 {_d1054ModbusAddress} 和 {_d1154ModbusAddress})...");
    }

    private async Task MonitorDRegistersLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ushort[] registers1 = await _modbusMaster.ReadHoldingRegistersAsync(_unitId, _d1054ModbusAddress, 2).ConfigureAwait(false);
                float value1 = (registers1 != null && registers1.Length == 2) ? UshortsToFloat(registers1) : float.NaN; // 使用 NaN 表示无效

                ushort[] registers2 = await _modbusMaster.ReadHoldingRegistersAsync(_unitId, _d1154ModbusAddress, 2).ConfigureAwait(false);
                float value2 = (registers2 != null && registers2.Length == 2) ? UshortsToFloat(registers2) : float.NaN;

                OnDRegisterFloatReadingsReceived?.Invoke(this, (value1, value2));
            }
            catch (OperationCanceledException) { Debug.WriteLine("Modbus: 持续读取 D 寄存器任务被取消。"); break; }
            catch (Exception ex) { Debug.WriteLine($"Modbus: 持续读取 D 寄存器时发生错误: {ex.Message}");  }

            try { await Task.Delay(_dRegisterPollIntervalMs, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { Debug.WriteLine("Modbus: 持续读取 D 寄存器任务轮询延迟被取消。"); break; }
        }
        Debug.WriteLine("Modbus: 持续读取 D 寄存器任务结束。");
    }

    public void StopContinuousDRegisterReading()
    {
        _continuousReadDRegistersCts?.Cancel();
        // 不在此处 Wait Task, Dispose 时处理
        _continuousReadDRegistersCts?.Dispose(); _continuousReadDRegistersCts = null;
        _continuousReadDRegistersTask = null; // 允许重新启动
    }


    // --- 持续读取 Coil (Bool) 的功能 - 任务 1 ---
    public void StartContinuousCoilReading(ushort startAddress, ushort numberOfCoils, int pollIntervalMs)
    {
        if (_continuousReadCoilsTask != null && !_continuousReadCoilsTask.IsCompleted) { Debug.WriteLine("Modbus: 持续读取 Coil 任务 1 已经在运行。"); return; }
        if (numberOfCoils == 0) { Debug.WriteLine("Modbus: 要读取的 Coil 数量 (任务1) 不能为 0。"); return; }
        EnsureConnected();
        _coilStartAddress = startAddress;
        _numberOfCoilsToRead = numberOfCoils;
        _coilPollIntervalMs = pollIntervalMs;
        _continuousReadCoilsCts = new CancellationTokenSource();
        _continuousReadCoilsTask = Task.Run(async () => await MonitorCoilsLoopAsync(_continuousReadCoilsCts.Token, startAddress), _continuousReadCoilsCts.Token);
        Debug.WriteLine($"Modbus: 开始持续读取 Coil 任务 1 (Modbus 地址 {_coilStartAddress}, 数量 {_numberOfCoilsToRead}) ...");
    }

    private async Task MonitorCoilsLoopAsync(CancellationToken cancellationToken, ushort startAddress)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool[] coils = await _modbusMaster.ReadCoilsAsync(_unitId, _coilStartAddress, _numberOfCoilsToRead).ConfigureAwait(false);

                if (coils != null && coils.Length == _numberOfCoilsToRead)
                {
                    if (coils[0]) // 监控的Coil变为true (假设只监控第一个)横轴
                    {
                        Debug.WriteLine($"Modbus (Task 1): Coil {_coilStartAddress} is TRUE. Writing to 9295 and 9353.");
                        if(startAddress == 9345) 
                        {
                            await WriteSingleCoilAsync(9297, true);
                            //await WriteSingleCoilAsync(9345, false);
                            StopContinuousCoilReading();
                            break;
                        }
                        // 确保 WriteSingleCoilAsync 返回 Task 并被 await
                        await WriteSingleCoilAsync(9295, false);
                        await WriteSingleCoilAsync(9353, false);
                        await WriteSingleCoilAsync(9292, false);
                        await WriteSingleCoilAsync(9352, false);
                        _isCompleted1 = true;
                        StopContinuousCoilReading(); // 停止当前任务
                        break; // 退出循环
                    }
                    OnCoilReadingsReceived?.Invoke(this, (_coilStartAddress, coils));
                }
                else { Debug.WriteLine($"Modbus (Task 1): 持续读取 Coil (Modbus {_coilStartAddress}) 失败，数据无效。"); }
            }
            catch (OperationCanceledException) { Debug.WriteLine("Modbus (Task 1): 持续读取 Coil 任务被取消。"); break; }
            catch (Exception ex) { Debug.WriteLine($"Modbus (Task 1): 持续读取 Coil 时发生错误: {ex.Message}"); }

            try { await Task.Delay(_coilPollIntervalMs, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { Debug.WriteLine("Modbus (Task 1): 持续读取 Coil 任务轮询延迟被取消。"); break; }
        }
        Debug.WriteLine("Modbus: 持续读取 Coil 任务 1 结束。");
    }

    public void StopContinuousCoilReading()
    {
        _continuousReadCoilsCts?.Cancel();
        _continuousReadCoilsCts?.Dispose(); _continuousReadCoilsCts = null;
        _continuousReadCoilsTask = null;
    }

    // --- 持续读取 Coil (Bool) 的功能 - 任务 2 ---
    public void StartContinuousCoilReading2(ushort startAddress, ushort numberOfCoils, int pollIntervalMs)
    {
        if (_continuousReadCoilsTask2 != null && !_continuousReadCoilsTask2.IsCompleted) { Debug.WriteLine("Modbus: 持续读取 Coil 任务 2 已经在运行。"); return; }
        if (numberOfCoils == 0) { Debug.WriteLine("Modbus: 要读取的 Coil 数量 (任务2) 不能为 0。"); return; }
        EnsureConnected();
        _coilStartAddress2 = startAddress;
        _numberOfCoilsToRead2 = numberOfCoils;
        _coilPollIntervalMs2 = pollIntervalMs;
        _continuousReadCoilsCts2 = new CancellationTokenSource();
        _continuousReadCoilsTask2 = Task.Run(async () => await MonitorCoilsLoop2Async(_continuousReadCoilsCts2.Token, startAddress), _continuousReadCoilsCts2.Token);
        Debug.WriteLine($"Modbus: 开始持续读取 Coil 任务 2 (Modbus 地址 {_coilStartAddress2}, 数量 {_numberOfCoilsToRead2}) ...");
    }

    private async Task MonitorCoilsLoop2Async(CancellationToken cancellationToken, ushort startAddress)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool[] coils = await _modbusMaster.ReadCoilsAsync(_unitId, _coilStartAddress2, _numberOfCoilsToRead2).ConfigureAwait(false);

                if (coils != null && coils.Length == _numberOfCoilsToRead2)
                {
                    if (coils[0]) // 监控的Coil变为true (假设只监控第一个)
                    {
                        if (startAddress == 9245)
                        {
                            await WriteSingleCoilAsync(9197, true);
                            //await WriteSingleCoilAsync(9245, true);
                            StopContinuousCoilReading2();
                            break;
                        }
                        Debug.WriteLine($"Modbus (Task 2): Coil {_coilStartAddress2} is TRUE. Writing to 9195 and 9253.");
                        // 确保 WriteSingleCoilAsync 返回 Task 并被 await
                        await WriteSingleCoilAsync(9195, false).ConfigureAwait(false);
                        await WriteSingleCoilAsync(9253, false).ConfigureAwait(false);
                        await WriteSingleCoilAsync(9192, false).ConfigureAwait(false);
                        await WriteSingleCoilAsync(9252, false).ConfigureAwait(false);
                        _isCompleted2 = true;
                        StopContinuousCoilReading2(); // 停止当前任务2
                        break; // 退出循环
                    }
                    OnCoilReadingsReceived2?.Invoke(this, (_coilStartAddress2, coils));
                }
                else { Debug.WriteLine($"Modbus (Task 2): 持续读取 Coil (Modbus {_coilStartAddress2}) 失败，数据无效。"); }
            }
            catch (OperationCanceledException) { Debug.WriteLine("Modbus (Task 2): 持续读取 Coil 任务被取消。"); break; }
            catch (Exception ex) { Debug.WriteLine($"Modbus (Task 2): 持续读取 Coil 时发生错误: {ex.Message}"); /* 考虑错误处理 */ }

            try { await Task.Delay(_coilPollIntervalMs2, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { Debug.WriteLine("Modbus (Task 2): 持续读取 Coil 任务轮询延迟被取消。"); break; }
        }
        Debug.WriteLine("Modbus: 持续读取 Coil 任务 2 结束。");
    }

    public void StopContinuousCoilReading2()
    {
        _continuousReadCoilsCts2?.Cancel();
        _continuousReadCoilsCts2?.Dispose(); _continuousReadCoilsCts2 = null;
        _continuousReadCoilsTask2 = null;
    }


    // --- 数据类型转换帮助方法  ---
    private ushort[] FloatToUshorts(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        ushort[] words = new ushort[2];
        // 默认小端字节序系统，PLC常见的存储方式可能是低字在前或高字在前
        // Modbus通常先传输低字节，再传输高字节
        // 如果PLC是高字在前 (e.g., CDAB for float AB CD)
        // words[0] = BitConverter.ToUInt16(bytes, 2); // High word
        // words[1] = BitConverter.ToUInt16(bytes, 0); // Low word
        // 如果PLC是低字在前 (e.g., ABCD for float AB CD) - 这是 BitConverter 的默认行为
        words[0] = BitConverter.ToUInt16(bytes, 0); // Low word
        words[1] = BitConverter.ToUInt16(bytes, 2); // High word
        return words; // 需要根据PLC实际情况调整或确认
    }
    private float UshortsToFloat(ushort[] ushorts)
    {
        if (ushorts == null || ushorts.Length < 2) throw new ArgumentException("Ushorts array must contain at least 2 elements.");
        byte[] bytes = new byte[4];
        // 假设 ushorts[0] 是低字, ushorts[1] 是高字 (对应上面的 FloatToUshorts 默认)
        Buffer.BlockCopy(BitConverter.GetBytes(ushorts[0]), 0, bytes, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(ushorts[1]), 0, bytes, 2, 2);
        // 如果PLC是高字在前，则 ushorts[0] 是高字, ushorts[1] 是低字
        // Buffer.BlockCopy(BitConverter.GetBytes(ushorts[1]), 0, bytes, 0, 2); // Low word from ushorts[1]
        // Buffer.BlockCopy(BitConverter.GetBytes(ushorts[0]), 0, bytes, 2, 2); // High word from ushorts[0]
        return BitConverter.ToSingle(bytes, 0); // 需要根据PLC实际情况调整或确认
    }
    private int UshortsToInt32(ushort[] ushorts)
    {
        if (ushorts == null || ushorts.Length < 2) throw new ArgumentException("Ushorts array must contain at least 2 elements.");
        byte[] bytes = new byte[4];
        // 默认：低字在前
        Buffer.BlockCopy(BitConverter.GetBytes(ushorts[0]), 0, bytes, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(ushorts[1]), 0, bytes, 2, 2);
        return BitConverter.ToInt32(bytes, 0); // 需要根据PLC实际情况调整或确认
    }


    // --- 辅助方法 ---
    private void EnsureConnected()
    {
        if (!IsConnected) throw new InvalidOperationException("PLC未连接");
        if (_modbusMaster == null) throw new InvalidOperationException("Modbus master is not initialized.");
    }

    private void HandleModbusError(Exception ex)
    {
        // 可在此处添加更复杂的错误处理，如日志记录或重连尝试
        Debug.WriteLine($"Modbus Error encountered: {ex.GetType().Name} - {ex.Message}");
    }

    // --- IDisposable 实现 ---
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 停止所有后台任务
            StopContinuousDRegisterReading();
            StopContinuousCoilReading();
            StopContinuousCoilReading2();
                     
            Disconnect(); // 确保连接被正确关闭
        }
    }

    // remove 方法，现在改为 async Task
    public async Task MoveAndMonitorAsync(float xTarget, float yTarget)
    {
        if (!IsConnected)
        {
            Debug.WriteLine("MoveAndMonitorAsync: PLC not connected. Attempting to connect...");
            try
            {
                await ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MoveAndMonitorAsync: Connection failed: {ex.Message}");
                return; // 连接失败则退出
            }
        }
        Debug.WriteLine("MoveAndMonitorAsync: PLC connected.");

        // 地址定义
        ushort xPositionAddress = 1104; // D1104 - X 目标位置
        ushort xSpeedAddress = 1106;    // D1106 - X 速度
        ushort xRunFlagAddress = 9295;  // M9295 - X 轴启动标志

        ushort yPositionAddress = 1004; // D1004 - Y 目标位置
        ushort ySpeedAddress = 1006;    // D1006 - Y 速度
        ushort yRunFlagAddress = 9195;  // M9195 - Y 轴启动标志

        // 完成标志地址 (假设)
        ushort xCompletionFlagAddress = 9353; // M9353 - X 轴完成标志
        ushort yCompletionFlagAddress = 9253; // M9253 - Y 轴完成标志 (假设)
        float commonSpeed = 20f;
        int coilPollInterval = 100; // ms

        _isCompleted1 = false;
        _isCompleted2 = false;

        try
        {
            // 启动 Y 轴
            Debug.WriteLine($"MoveAndMonitorAsync: Setting Y target to {yTarget}, speed to {commonSpeed}.");
            WriteFloat(yPositionAddress, yTarget);       // 同步写入
            WriteFloat(ySpeedAddress, commonSpeed);      // 同步写入
            Debug.WriteLine($"MoveAndMonitorAsync: Starting Y axis (Coil {yRunFlagAddress}).");
            await WriteSingleCoilAsync(yRunFlagAddress, true).ConfigureAwait(false); // 启动 Y 轴

            // 启动 X 轴
            Debug.WriteLine($"MoveAndMonitorAsync: Setting X target to {xTarget}, speed to {commonSpeed}.");
            WriteFloat(xPositionAddress, xTarget);       // 同步写入
            WriteFloat(xSpeedAddress, commonSpeed);      // 同步写入
            Debug.WriteLine($"MoveAndMonitorAsync: Starting X axis (Coil {xRunFlagAddress}).");
            await WriteSingleCoilAsync(xRunFlagAddress, true).ConfigureAwait(false); // 启动 X 轴


            // 启动监控任务
            Debug.WriteLine($"MoveAndMonitorAsync: Starting monitoring for X completion (Coil {xCompletionFlagAddress}).");
            StartContinuousCoilReading(xCompletionFlagAddress, 1, coilPollInterval); // 监控X轴完成

            Debug.WriteLine($"MoveAndMonitorAsync: Starting monitoring for Y completion (Coil {yCompletionFlagAddress}).");
            StartContinuousCoilReading2(yCompletionFlagAddress, 1, coilPollInterval); // 监控Y轴完成
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MoveAndMonitorAsync: Error during movement/monitoring setup: {ex.Message}");
            // 发生错误时，可能需要停止已启动的监控
            StopContinuousCoilReading();
            StopContinuousCoilReading2();
        }
    }


    public void getdata()
    {
        
        LeftValue.Clear();
        RightValue.Clear();

        object left1 = ReadFloat(1000);
        object left2 = ReadFloat(1002);
        object left3 = ReadFloat(1004);
        object left4 = ReadFloat(1006);
        object left5 = ReadFloat(1050);
        object left6 = ReadFloat(1052);
        object left7 = ReadFloat(1054);
        
        object left8 = _modbusMaster.ReadCoils(1, 9192, 1)[0];
        object left9 = _modbusMaster.ReadCoils(1, 9193, 1)[0];
        object left10 = _modbusMaster.ReadCoils(1, 9194, 1)[0];
        object left11 = _modbusMaster.ReadCoils(1, 9195, 1)[0]; 
        object left12 = _modbusMaster.ReadCoils(1, 9196, 1)[0];
        object left13 = _modbusMaster.ReadCoils(1, 9196, 1)[0];

        object left14 = _modbusMaster.ReadCoils(1, 9242, 1)[0];
        object left15 = _modbusMaster.ReadCoils(1, 9243, 1)[0];
        object left16 = _modbusMaster.ReadCoils(1, 9244, 1)[0];
        object left17 = _modbusMaster.ReadCoils(1, 9245, 1)[0];
        object left18 = _modbusMaster.ReadCoils(1, 9246, 1)[0];

        object left19 = _modbusMaster.ReadCoils(1, 9252, 1)[0];
        object left20 = _modbusMaster.ReadCoils(1, 9253, 1)[0];

        object left21 = _modbusMaster.ReadCoils(1, 9392, 1)[0]; 
        object left22 = _modbusMaster.ReadCoils(1, 9393, 1)[0];
        object left23 = ReadFloat(1200);
        LeftValue.Add(left1);
        LeftValue.Add(left2);
        LeftValue.Add(left3);
        LeftValue.Add(left4);
        LeftValue.Add(left5);
        LeftValue.Add(left6);
        LeftValue.Add(left7);
        LeftValue.Add(left8);
        LeftValue.Add(left9);
        LeftValue.Add(left10);
        LeftValue.Add(left11);
        LeftValue.Add(left12);
        LeftValue.Add(left13);
        LeftValue.Add(left14);
        LeftValue.Add(left15);
        LeftValue.Add(left16);
        LeftValue.Add(left17);
        LeftValue.Add(left18);
        LeftValue.Add(left19);
        LeftValue.Add(left20);
        LeftValue.Add(left21);
        LeftValue.Add(left22);
        LeftValue.Add(left23);

        object right1 = ReadFloat(1100);
        object right2 = ReadFloat(1102);
        object right3 = ReadFloat(1104);
        object right4 = ReadFloat(1106);
        object right5 = ReadFloat(1150);
        object right6 = ReadFloat(1152);
        object right7 = ReadFloat(1154);

        object right8 = _modbusMaster.ReadCoils(1, 9292, 1)[0];
        object right9 = _modbusMaster.ReadCoils(1, 9293, 1)[0];
        object right10 = _modbusMaster.ReadCoils(1, 9294, 1)[0];
        object right11 = _modbusMaster.ReadCoils(1, 9295, 1)[0];
        object right12 = _modbusMaster.ReadCoils(1, 9296, 1)[0];
        object right13 = _modbusMaster.ReadCoils(1, 9296, 1)[0];

        object right14 = _modbusMaster.ReadCoils(1, 9342, 1)[0];
        object right15 = _modbusMaster.ReadCoils(1, 9343, 1)[0];
        object right16 = _modbusMaster.ReadCoils(1, 9344, 1)[0];
        object right17 = _modbusMaster.ReadCoils(1, 9345, 1)[0];
        object right18 = _modbusMaster.ReadCoils(1, 9346, 1)[0];

        object right19 = _modbusMaster.ReadCoils(1, 9352, 1)[0];
        object right20 = _modbusMaster.ReadCoils(1, 9353, 1)[0];

        RightValue.Add(right1);
        RightValue.Add(right2);
        RightValue.Add(right3);
        RightValue.Add(right4);
        RightValue.Add(right5);
        RightValue.Add(right6);
        RightValue.Add(right7);
        RightValue.Add(right8);
        RightValue.Add(right9);
        RightValue.Add(right10);
        RightValue.Add(right11);
        RightValue.Add(right12);
        RightValue.Add(right13);
        RightValue.Add(right14);
        RightValue.Add(right15);
        RightValue.Add(right16);
        RightValue.Add(right17);
        RightValue.Add(right18);
        RightValue.Add(right19);
        RightValue.Add(right20);
             

    }




}
