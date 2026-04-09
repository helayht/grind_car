using System;
using System.Threading.Tasks;

namespace GrindCar.Services;

/// <summary>
/// 定义 PLC 客户端的基本连接、读写与释放能力。
/// </summary>
public interface IPlcClient : IDisposable
{
    /// <summary>
    /// 获取当前 PLC 连接是否处于可用状态。
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 异步建立与 PLC 的连接。
    /// </summary>
    /// <returns>表示异步连接操作的任务。</returns>
    Task ConnectAsync();

    /// <summary>
    /// 断开与 PLC 的连接并释放底层通信资源。
    /// </summary>
    void Disconnect();

    /// <summary>
    /// 从指定起始寄存器读取一个 32 位整型值。
    /// </summary>
    /// <param name="startAddress">32 位整型值对应的起始寄存器地址。</param>
    /// <returns>读取到的 32 位整型值。</returns>
    int ReadInt32(ushort startAddress);

    /// <summary>
    /// 将一个 32 位整型值写入指定起始寄存器。
    /// </summary>
    /// <param name="startAddress">32 位整型值对应的起始寄存器地址。</param>
    /// <param name="value">要写入的整型值。</param>
    void WriteInt32(ushort startAddress, int value);

    /// <summary>
    /// 将一个 16 位整型值写入指定寄存器。
    /// </summary>
    /// <param name="address">目标寄存器地址。</param>
    /// <param name="value">要写入的 16 位整型值。</param>
    void WriteInt16(ushort address, short value);

    /// <summary>
    /// 异步写入单个 Coil 布尔值。
    /// </summary>
    /// <param name="coilAddress">目标 Coil 地址。</param>
    /// <param name="value">要写入的布尔值。</param>
    /// <returns>表示异步写入操作的任务。</returns>
    Task WriteSingleCoilAsync(ushort coilAddress, bool value);
}
