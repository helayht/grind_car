using System;

namespace GrindCar.Services.Motor;

/// <summary>
/// PLC 连接参数校验结果。
/// </summary>
public readonly struct PlcConnectionValidationResult
{
    private PlcConnectionValidationResult(bool isValid, string ipAddress, int port, string errorMessage)
    {
        IsValid = isValid;
        IpAddress = ipAddress;
        Port = port;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }

    public string IpAddress { get; }

    public int Port { get; }

    public string ErrorMessage { get; }

    public static PlcConnectionValidationResult Success(string ipAddress, int port)
    {
        return new PlcConnectionValidationResult(true, ipAddress, port, string.Empty);
    }

    public static PlcConnectionValidationResult Fail(string errorMessage)
    {
        return new PlcConnectionValidationResult(false, string.Empty, 0, errorMessage);
    }
}