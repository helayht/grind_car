using System;
using System.Threading.Tasks;

namespace GrindCar.Services;

public interface IPlcClient : IDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync();
    void Disconnect();
    int ReadInt32(ushort startAddress);
    void WriteInt32(ushort startAddress, int value);
    void WriteInt16(ushort address, short value);
    Task WriteSingleCoilAsync(ushort coilAddress, bool value);
}
