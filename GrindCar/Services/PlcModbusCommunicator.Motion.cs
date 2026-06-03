using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GrindCar.Services;

public partial class PlcModbusCommunicator
{
    /// <summary>
    /// 写入 X、Y 轴目标位置和速度，并启动完成状态监控。
    /// </summary>
    /// <param name="xTarget">X 轴目标位置。</param>
    /// <param name="yTarget">Y 轴目标位置。</param>
    /// <returns>表示移动启动流程的异步任务。</returns>
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
                throw;
            }
        }

        Debug.WriteLine("MoveAndMonitorAsync: PLC connected.");

        ushort xPositionAddress = MoveXPositionAddress;
        ushort xSpeedAddress = MoveXSpeedAddress;
        ushort xRunFlagAddress = MoveXRunFlagAddress;

        ushort yPositionAddress = MoveYPositionAddress;
        ushort ySpeedAddress = MoveYSpeedAddress;
        ushort yRunFlagAddress = MoveYRunFlagAddress;

        ushort xCompletionFlagAddress = MoveXCompletionFlagAddress;
        ushort yCompletionFlagAddress = MoveYCompletionFlagAddress;
        float commonSpeed = DefaultCommonSpeed;
        int coilPollInterval = DefaultCoilPollIntervalMs;

        _isCompleted1 = false;
        _isCompleted2 = false;
        Debug.WriteLine($"MoveAndMonitorAsync: Completion flags reset. X={_isCompleted1}, Y={_isCompleted2}.");
        bool xStarted = false;
        bool yStarted = false;

        try
        {
            Debug.WriteLine($"MoveAndMonitorAsync: Setting Y target to {yTarget}, speed to {commonSpeed}.");
            WriteFloat(yPositionAddress, yTarget);
            WriteFloat(ySpeedAddress, commonSpeed);
            Debug.WriteLine($"MoveAndMonitorAsync: Starting Y axis (Coil {yRunFlagAddress}).");
            await WriteSingleCoilAsync(yRunFlagAddress, true).ConfigureAwait(false);
            yStarted = true;

            Debug.WriteLine($"MoveAndMonitorAsync: Setting X target to {xTarget}, speed to {commonSpeed}.");
            WriteFloat(xPositionAddress, xTarget);
            WriteFloat(xSpeedAddress, commonSpeed);
            Debug.WriteLine($"MoveAndMonitorAsync: Starting X axis (Coil {xRunFlagAddress}).");
            await WriteSingleCoilAsync(xRunFlagAddress, true).ConfigureAwait(false);
            xStarted = true;

            Debug.WriteLine($"MoveAndMonitorAsync: Starting monitoring for X completion (Coil {xCompletionFlagAddress}).");
            StartContinuousCoilReading(xCompletionFlagAddress, 1, coilPollInterval);

            Debug.WriteLine($"MoveAndMonitorAsync: Starting monitoring for Y completion (Coil {yCompletionFlagAddress}).");
            StartContinuousCoilReading2(yCompletionFlagAddress, 1, coilPollInterval);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MoveAndMonitorAsync: Error during movement/monitoring setup: {ex.Message}");
            StopContinuousCoilReading();
            StopContinuousCoilReading2();
            await StopStartedAxisAsync(xRunFlagAddress, xStarted, "X").ConfigureAwait(false);
            await StopStartedAxisAsync(yRunFlagAddress, yStarted, "Y").ConfigureAwait(false);
            throw;
        }
    }

    private async Task StopStartedAxisAsync(ushort runFlagAddress, bool isStarted, string axisName)
    {
        if (!isStarted)
        {
            return;
        }

        try
        {
            Debug.WriteLine($"MoveAndMonitorAsync: Stopping {axisName} axis (Coil {runFlagAddress}).");
            await WriteSingleCoilAsync(runFlagAddress, false).ConfigureAwait(false);
        }
        catch (Exception stopException)
        {
            Debug.WriteLine(
                $"MoveAndMonitorAsync: Failed to stop {axisName} axis (Coil {runFlagAddress}): {stopException.Message}");
        }
    }

    /// <summary>
    /// 读取预定义左右两组地址的数据并缓存在公开列表中。
    /// </summary>
    public void GetData()
    {
        EnsureConnected();
        LeftValue.Clear();
        RightValue.Clear();

        for (int index = 0; index < LeftFloatAddresses.Length; index++)
        {
            LeftValue.Add(ReadFloat(LeftFloatAddresses[index]));
        }

        for (int index = 0; index < LeftCoilAddresses.Length; index++)
        {
            LeftValue.Add(ReadSingleCoil(LeftCoilAddresses[index]));
        }

        LeftValue.Add(ReadFloat(LeftTailFloatAddress));

        for (int index = 0; index < RightFloatAddresses.Length; index++)
        {
            RightValue.Add(ReadFloat(RightFloatAddresses[index]));
        }

        for (int index = 0; index < RightCoilAddresses.Length; index++)
        {
            RightValue.Add(ReadSingleCoil(RightCoilAddresses[index]));
        }
    }

    /// <summary>
    /// 兼容旧调用入口，等价于 <see cref="GetData"/>。
    /// </summary>
    public void getdata()
    {
        GetData();
    }
}
