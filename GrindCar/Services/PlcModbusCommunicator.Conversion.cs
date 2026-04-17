using System;

namespace GrindCar.Services;

public partial class PlcModbusCommunicator
{
    // --- 数据类型转换帮助方法  ---
    /// <summary>
    /// 将浮点值转换为两个 16 位寄存器值。
    /// </summary>
    /// <param name="value">待转换的浮点值。</param>
    /// <returns>转换后的寄存器数组。</returns>
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
    /// <summary>
    /// 将两个 16 位寄存器值还原为浮点数。
    /// </summary>
    /// <param name="ushorts">寄存器数组。</param>
    /// <returns>还原得到的浮点值。</returns>
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
    /// <summary>
    /// 将两个 16 位寄存器值还原为 32 位整型。
    /// </summary>
    /// <param name="ushorts">寄存器数组。</param>
    /// <returns>还原得到的整型值。</returns>
    private int UshortsToInt32(ushort[] ushorts)
    {
        if (ushorts == null || ushorts.Length < 2) throw new ArgumentException("Ushorts array must contain at least 2 elements.");
        byte[] bytes = new byte[4];
        // 默认：低字在前
        Buffer.BlockCopy(BitConverter.GetBytes(ushorts[0]), 0, bytes, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(ushorts[1]), 0, bytes, 2, 2);
        return BitConverter.ToInt32(bytes, 0); // 需要根据PLC实际情况调整或确认
    }

    /// <summary>
    /// 将 32 位整型拆分为两个 16 位寄存器值。
    /// </summary>
    /// <param name="value">待转换的整型值。</param>
    /// <returns>转换后的寄存器数组。</returns>
    private ushort[] Int32ToUshorts(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        ushort[] words = new ushort[2];
        words[0] = BitConverter.ToUInt16(bytes, 0); // Low word
        words[1] = BitConverter.ToUInt16(bytes, 2); // High word
        return words;
    }
}
