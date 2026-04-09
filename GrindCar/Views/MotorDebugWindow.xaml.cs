using System.Windows;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 电机调试窗口。
/// </summary>
public partial class MotorDebugWindow : Window
{
    private readonly MotorViewModel _viewModel = new();

    /// <summary>
    /// 初始化电机调试窗口，并绑定视图模型及失败提示事件。
    /// </summary>
    public MotorDebugWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Unloaded += MotorDebugWindow_Unloaded;
        _viewModel.ConnectionFailed += OnConnectionFailed;
    }

    /// <summary>
    /// 在窗口卸载时停止后台轮询与连接。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">窗口卸载事件参数。</param>
    private void MotorDebugWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Shutdown();
    }

    /// <summary>
    /// 在界面线程中弹出 PLC 连接失败提示。
    /// </summary>
    /// <param name="message">需要展示的错误消息。</param>
    private void OnConnectionFailed(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(this, message, "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }
}
