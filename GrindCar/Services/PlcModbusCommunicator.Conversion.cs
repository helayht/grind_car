using System;

namespace GrindCar.Services;

public partial class PlcModbusCommunicator
{
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
