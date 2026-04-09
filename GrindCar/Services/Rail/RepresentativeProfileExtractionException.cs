using System;

namespace GrindCar.Services.Rail;

/// <summary>
/// 表示代表截面或中位截面提取过程中发生的异常。
/// </summary>
public sealed class RepresentativeProfileExtractionException : Exception
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">异常说明信息。</param>
    public RepresentativeProfileExtractionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用指定错误消息和内部异常初始化异常。
    /// </summary>
    /// <param name="message">异常说明信息。</param>
    /// <param name="innerException">导致当前异常的内部异常。</param>
    public RepresentativeProfileExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
