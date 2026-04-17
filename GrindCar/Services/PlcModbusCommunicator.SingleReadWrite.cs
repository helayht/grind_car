using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Modbus.Device;

namespace GrindCar.Services;

public partial class PlcModbusCommunicator
{
    /// <summary>
    /// 向指定 Coil 地址写入一个布尔值。
    /// </summary>
    /// <param name="coilAddress">目标 Coil 地址。</param>
    /// <param name="value">要写入的布尔值。</param>
    public void WriteSingleCoil(ushort coilAddress, bool value)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            modbusMaster.WriteSingleCoil(1, coilAddress, value);
            Debug.WriteLine($"Modbus: Wrote Coil {coilAddress}={value}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Err Wr Coil {coilAddress}:{ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 读取指定 Coil 地址的布尔值。
    /// </summary>
    /// <param name="coilAddress">目标 Coil 地址。</param>
    /// <returns>读取到的布尔值。</returns>
    public bool ReadSingleCoil(ushort coilAddress)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            bool[] response = modbusMaster.ReadCoils(1, coilAddress, 1);
            if (response != null && response.Length > 0)
            {
                return response[0];
            }

            throw new Modbus.SlaveException();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Err Rd Coil {coilAddress}:{ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 写入单个 Modbus Coil (异步, 返回 Task)。
    /// </summary>
    /// <param name="coilAddress">目标 Coil 地址。</param>
    /// <param name="value">要写入的布尔值。</param>
    /// <returns>表示异步写入操作的任务。</returns>
    public async Task WriteSingleCoilAsync(ushort coilAddress, bool value)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            await modbusMaster.WriteSingleCoilAsync(_unitId, coilAddress, value).ConfigureAwait(false);
            Debug.WriteLine($"Modbus: 成功写入布尔值 {value} 到 Coil 地址 {coilAddress}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 写入 Coil 地址 {coilAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 将浮点值写入两个连续 Holding Register。
    /// </summary>
    /// <param name="startAddress">起始寄存器地址。</param>
    /// <param name="value">要写入的浮点值。</param>
    public void WriteFloat(ushort startAddress, float value)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            ushort[] dataToWrite = FloatToUshorts(value);
            modbusMaster.WriteMultipleRegisters(_unitId, startAddress, dataToWrite);
            Debug.WriteLine($"Modbus: 成功写入浮点数 {value} 到 Holding Register 地址 {startAddress}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 写入 Float 到 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 从两个连续 Holding Register 读取一个浮点值。
    /// </summary>
    /// <param name="startAddress">起始寄存器地址。</param>
    /// <returns>读取到的浮点值。</returns>
    public float ReadFloat(ushort startAddress)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            ushort[] registers = modbusMaster.ReadHoldingRegisters(_unitId, startAddress, 2);
            if (registers != null && registers.Length == 2)
            {
                float value = UshortsToFloat(registers);
                Debug.WriteLine($"Modbus: 成功读取浮点数 {value} 从 Holding Register 地址 {startAddress}");
                return value;
            }

            Debug.WriteLine($"Modbus: 从 Holding Register 地址 {startAddress} 读取 Float 失败，未返回足够数据。");
            throw new Exception($"Failed to read 2 registers for Float from address {startAddress}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 读取 Float 从 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 从两个连续 Holding Register 读取一个 32 位整型值。
    /// </summary>
    /// <param name="startAddress">起始寄存器地址。</param>
    /// <returns>读取到的整型值。</returns>
    public int ReadInt32(ushort startAddress)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            ushort[] registers = modbusMaster.ReadHoldingRegisters(_unitId, startAddress, 2);
            if (registers != null && registers.Length == 2)
            {
                int value = UshortsToInt32(registers);
                Debug.WriteLine($"Modbus: 成功读取整型 {value} 从 Holding Register 地址 {startAddress}");
                return value;
            }

            Debug.WriteLine($"Modbus: 从 Holding Register 地址 {startAddress} 读取 Int32 失败，未返回足够数据。");
            throw new Exception($"Failed to read 2 registers for Int32 from address {startAddress}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 读取 Int32 从 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 将一个 32 位整型值写入两个连续 Holding Register。
    /// </summary>
    /// <param name="startAddress">起始寄存器地址。</param>
    /// <param name="value">要写入的整型值。</param>
    public void WriteInt32(ushort startAddress, int value)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            ushort[] dataToWrite = Int32ToUshorts(value);
            modbusMaster.WriteMultipleRegisters(_unitId, startAddress, dataToWrite);
            Debug.WriteLine($"Modbus: 成功写入整型 {value} 到 Holding Register 地址 {startAddress}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 写入 Int32 到 Holding Register 地址 {startAddress} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 写入 16 位整型（单寄存器）。
    /// </summary>
    /// <param name="address">目标寄存器地址。</param>
    /// <param name="value">要写入的 16 位整型值。</param>
    public void WriteInt16(ushort address, short value)
    {
        EnsureConnected();
        IModbusMaster modbusMaster = GetModbusMaster();
        try
        {
            ushort raw = unchecked((ushort)value);
            modbusMaster.WriteSingleRegister(_unitId, address, raw);
            Debug.WriteLine($"Modbus: 成功写入 Int16 {value} 到 Holding Register 地址 {address}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Modbus: 写入 Int16 到 Holding Register 地址 {address} 发生错误: {ex.Message}");
            HandleModbusError(ex);
            throw;
        }
    }

    /// <summary>
    /// 校验当前连接和 Modbus 主站是否可用。
    /// </summary>
    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("PLC未连接");
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized.");
        }
    }

    /// <summary>
    /// 获取当前可用的 Modbus 主站实例。
    /// </summary>
    /// <returns>当前连接对应的 Modbus 主站对象。</returns>
    private IModbusMaster GetModbusMaster()
    {
        EnsureConnected();
        return _modbusMaster!;
    }

    /// <summary>
    /// 统一记录 Modbus 调用异常信息。
    /// </summary>
    /// <param name="ex">捕获到的异常对象。</param>
    private void HandleModbusError(Exception ex)
    {
        Debug.WriteLine($"Modbus Error encountered: {ex.GetType().Name} - {ex.Message}");
    }
}
