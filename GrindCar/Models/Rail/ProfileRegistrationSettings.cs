using System;

namespace GrindCar.Models.Rail;

/// <summary>
/// Left/Right 代表廓形手动配准参数。
/// </summary>
public sealed class ProfileRegistrationSettings
{
    public ProfileRegistrationParameters? Left { get; set; }

    public ProfileRegistrationParameters? Right { get; set; }

    public ProfileRegistrationParameters GetParameters(PointCloudDeviceSide side)
    {
        ProfileRegistrationParameters? parameters = side == PointCloudDeviceSide.Left ? Left : Right;
        if (parameters == null)
        {
            throw new InvalidOperationException($"配准配置缺少 {side} 参数。");
        }

        return parameters;
    }

    public void Validate()
    {
        if (Left == null)
        {
            throw new InvalidOperationException("配准配置缺少 Left 参数。");
        }

        if (Right == null)
        {
            throw new InvalidOperationException("配准配置缺少 Right 参数。");
        }

        Left.Validate("Left");
        Right.Validate("Right");
    }
}
