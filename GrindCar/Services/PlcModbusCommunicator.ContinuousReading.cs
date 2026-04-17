using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Modbus.Device;

namespace GrindCar.Services;

public partial class PlcModbusCommunicator
{
    // --- 持续读取 D 寄存器 (Float) 的功能  ---
    /// <summary>
    /// 启动持续读取两个 D 寄存器浮点值的后台任务。
    /// </summary>
    /// <param name="d1054ModbusAddress">第一个寄存器起始地址。</param>
    /// <param name="d1154ModbusAddress">第二个寄存器起始地址。</param>
    /// <param name="pollIntervalMs">轮询间隔，单位毫秒。</param>
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

    /// <summary>
    /// 后台循环读取两个 D 寄存器并通过事件发布结果。
    /// </summary>
    /// <param name="cancellationToken">用于取消轮询任务的令牌。</param>
    /// <returns>表示监控循环生命周期的任务。</returns>
    private async Task MonitorDRegistersLoopAsync(CancellationToken cancellationToken)
    {
        IModbusMaster modbusMaster = GetModbusMaster();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ushort[] registers1 = await modbusMaster.ReadHoldingRegistersAsync(_unitId, _d1054ModbusAddress, 2).ConfigureAwait(false);
                float value1 = (registers1 != null && registers1.Length == 2) ? UshortsToFloat(registers1) : float.NaN; // 使用 NaN 表示无效

                ushort[] registers2 = await modbusMaster.ReadHoldingRegistersAsync(_unitId, _d1154ModbusAddress, 2).ConfigureAwait(false);
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

    /// <summary>
    /// 停止持续读取 D 寄存器的后台任务。
    /// </summary>
    public void StopContinuousDRegisterReading()
    {
        _continuousReadDRegistersCts?.Cancel();
        // 不在此处 Wait Task, Dispose 时处理
        _continuousReadDRegistersCts?.Dispose(); _continuousReadDRegistersCts = null;
        _continuousReadDRegistersTask = null; // 允许重新启动
    }


    // --- 持续读取 Coil (Bool) 的功能 - 任务 1 ---
    /// <summary>
    /// 启动第一组 Coil 的持续读取任务。
    /// </summary>
    /// <param name="startAddress">起始 Coil 地址。</param>
    /// <param name="numberOfCoils">读取的 Coil 数量。</param>
    /// <param name="pollIntervalMs">轮询间隔，单位毫秒。</param>
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

    /// <summary>
    /// 后台轮询第一组 Coil，并在满足条件时执行联动写入。
    /// </summary>
    /// <param name="cancellationToken">用于取消轮询任务的令牌。</param>
    /// <param name="startAddress">监控起始地址。</param>
    /// <returns>表示监控循环生命周期的任务。</returns>
    private async Task MonitorCoilsLoopAsync(CancellationToken cancellationToken, ushort startAddress)
    {
        IModbusMaster modbusMaster = GetModbusMaster();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool[] coils = await modbusMaster.ReadCoilsAsync(_unitId, _coilStartAddress, _numberOfCoilsToRead).ConfigureAwait(false);

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

    /// <summary>
    /// 停止第一组 Coil 的持续读取任务。
    /// </summary>
    public void StopContinuousCoilReading()
    {
        _continuousReadCoilsCts?.Cancel();
        _continuousReadCoilsCts?.Dispose(); _continuousReadCoilsCts = null;
        _continuousReadCoilsTask = null;
    }

    // --- 持续读取 Coil (Bool) 的功能 - 任务 2 ---
    /// <summary>
    /// 启动第二组 Coil 的持续读取任务。
    /// </summary>
    /// <param name="startAddress">起始 Coil 地址。</param>
    /// <param name="numberOfCoils">读取的 Coil 数量。</param>
    /// <param name="pollIntervalMs">轮询间隔，单位毫秒。</param>
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

    /// <summary>
    /// 后台轮询第二组 Coil，并在满足条件时执行联动写入。
    /// </summary>
    /// <param name="cancellationToken">用于取消轮询任务的令牌。</param>
    /// <param name="startAddress">监控起始地址。</param>
    /// <returns>表示监控循环生命周期的任务。</returns>
    private async Task MonitorCoilsLoop2Async(CancellationToken cancellationToken, ushort startAddress)
    {
        IModbusMaster modbusMaster = GetModbusMaster();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool[] coils = await modbusMaster.ReadCoilsAsync(_unitId, _coilStartAddress2, _numberOfCoilsToRead2).ConfigureAwait(false);

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

    /// <summary>
    /// 停止第二组 Coil 的持续读取任务。
    /// </summary>
    public void StopContinuousCoilReading2()
    {
        _continuousReadCoilsCts2?.Cancel();
        _continuousReadCoilsCts2?.Dispose(); _continuousReadCoilsCts2 = null;
        _continuousReadCoilsTask2 = null;
    }
}
