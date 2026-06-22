using System;

namespace GrindCar.Models.Rail;

/// <summary>
/// 单侧代表廓形手动配准参数。
/// </summary>
public sealed class ProfileRegistrationParameters
{
    public ProfileRegistrationParameters()
    {
    }

    public ProfileRegistrationParameters(double dx, double dy, double rotationDegrees, bool isMirrored = false)
    {
        Dx = dx;
        Dy = dy;
        RotationDegrees = rotationDegrees;
        IsMirrored = isMirrored;
    }

    public double Dx { get; set; }

    public double Dy { get; set; }

    public double RotationDegrees { get; set; }

    public bool IsMirrored { get; set; }

    public void Validate(string sideName)
    {
        if (!IsFinite(Dx))
        {
            throw new InvalidOperationException($"{sideName} 配准参数 Dx 必须是有效数字。");
        }

        if (!IsFinite(Dy))
        {
            throw new InvalidOperationException($"{sideName} 配准参数 Dy 必须是有效数字。");
        }

        if (!IsFinite(RotationDegrees))
        {
            throw new InvalidOperationException($"{sideName} 配准参数 RotationDegrees 必须是有效数字。");
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
