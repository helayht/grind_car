using System;

namespace GrindCar.Services.PointCloud;

/// <summary>
/// 表示调用点云设备 SDK 过程中发生的业务异常。
/// </summary>
public sealed class PointCloudSdkException : Exception
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">异常说明信息。</param>
    public PointCloudSdkException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用指定错误消息和内部异常初始化异常。
    /// </summary>
    /// <param name="message">异常说明信息。</param>
    /// <param name="innerException">导致当前异常的内部异常。</param>
    public PointCloudSdkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
