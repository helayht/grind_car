using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GrindCar.ViewModels;

/// <summary>
/// 表示单个电机参数项在界面中的显示与输入状态。
/// </summary>
public sealed class MotorParameterItemViewModel : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private string _inputValue = string.Empty;
    private bool _boolValue;

    /// <summary>
    /// 初始化参数项视图模型。
    /// </summary>
    /// <param name="name">参数名称。</param>
    /// <param name="isReadOnly">是否为只读参数。</param>
    /// <param name="unit">参数显示单位。</param>
    /// <param name="isBoolWrite">是否为布尔写入参数。</param>
    public MotorParameterItemViewModel(string name, bool isReadOnly, string unit, bool isBoolWrite = false)
    {
        Name = name;
        IsReadOnly = isReadOnly;
        Unit = unit;
        IsBoolWrite = isBoolWrite;
    }

    public string Name { get; }
    public bool IsReadOnly { get; }
    public string Unit { get; }
    public bool IsBoolWrite { get; }
    public bool IsValueWrite => !IsReadOnly && !IsBoolWrite;
    public string BoolToggleText => _boolValue ? "复位" : "置位";

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (_boolValue == value) return;
            _boolValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BoolToggleText));
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public string InputValue
    {
        get => _inputValue;
        set
        {
            if (_inputValue == value) return;
            _inputValue = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发属性变更通知，驱动界面刷新。
    /// </summary>
    /// <param name="propertyName">发生变化的属性名称。</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
