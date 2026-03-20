using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GrindCar.ViewModels;

public sealed class MotorParameterItemViewModel : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private string _inputValue = string.Empty;

    public MotorParameterItemViewModel(string name, bool isReadOnly, string unit)
    {
        Name = name;
        IsReadOnly = isReadOnly;
        Unit = unit;
    }

    public string Name { get; }
    public bool IsReadOnly { get; }
    public string Unit { get; }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
