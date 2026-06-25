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

    /// <summary>
    /// 裁切曲线的X范围下限（配准后坐标系）。null 表示不限制。
    /// </summary>
    public double? XMin { get; set; }

    /// <summary>
    /// 裁切曲线的X范围上限（配准后坐标系）。null 表示不限制。
    /// </summary>
    public double? XMax { get; set; }

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

        if (XMin.HasValue && !IsFinite(XMin.Value))
        {
            throw new InvalidOperationException($"{sideName} 配准参数 XMin 必须是有效数字。");
        }

        if (XMax.HasValue && !IsFinite(XMax.Value))
        {
            throw new InvalidOperationException($"{sideName} 配准参数 XMax 必须是有效数字。");
        }

        if (XMin.HasValue && XMax.HasValue && XMin.Value > XMax.Value)
        {
            throw new InvalidOperationException($"{sideName} 配准参数 XMin 不能大于 XMax。");
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
