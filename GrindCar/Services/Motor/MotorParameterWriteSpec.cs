namespace GrindCar.Services.Motor;

/// <summary>
/// 电机参数写入规格。
/// </summary>
public readonly struct MotorParameterWriteSpec
{
    public MotorParameterWriteSpec(ushort address, MotorParameterDataKind kind, double scale)
    {
        Address = address;
        Kind = kind;
        Scale = scale;
    }

    public ushort Address { get; }

    public MotorParameterDataKind Kind { get; }

    public double Scale { get; }
}