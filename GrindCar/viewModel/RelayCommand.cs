using System;
using System.Windows.Input;

namespace GrindCar.viewModel;

/// <summary>
/// 通用命令封装：将委托包装为 ICommand 以便 XAML 绑定
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    /// <summary>
    /// 创建命令实例
    /// </summary>
    /// <param name="execute">执行逻辑</param>
    /// <param name="canExecute">可执行判断（可选）</param>
    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 判断是否可执行
    /// </summary>
    public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// 执行命令
    /// </summary>
    public void Execute(object parameter) => _execute(parameter);

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
