using System;

namespace GrindCar.Services.Motor;

/// <summary>
/// 校验 PLC 连接参数。
/// </summary>
public static class PlcConnectionSettingsValidator
{
    public const int MinPort = 1;

    public const int MaxPort = 65535;

    public static PlcConnectionValidationResult Validate(string? ipAddress, string? portText)
    {
        string normalizedIpAddress = ipAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedIpAddress))
        {
            return PlcConnectionValidationResult.Fail("IP 不能为空。");
        }

        string normalizedPortText = portText?.Trim() ?? string.Empty;
        if (!int.TryParse(normalizedPortText, out int port) || port < MinPort || port > MaxPort)
        {
            return PlcConnectionValidationResult.Fail($"端口无效，请输入 {MinPort}-{MaxPort} 的整数。");
        }

        return PlcConnectionValidationResult.Success(normalizedIpAddress, port);
    }
}