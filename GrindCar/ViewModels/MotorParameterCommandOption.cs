namespace GrindCar.ViewModels;

/// <summary>
/// 表示电机参数固定指令按钮选项。
/// </summary>
/// <param name="ParameterName">参数名称。</param>
/// <param name="DisplayName">按钮显示名称。</param>
/// <param name="Value">写入 PLC 的指令值。</param>
public readonly record struct MotorParameterCommandOption(
    string ParameterName,
    string DisplayName,
    short Value);
