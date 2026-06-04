using System.Windows;
using GrindCar.Services;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 电机调试窗口。
/// </summary>
public partial class MotorDebugWindow : Window
{
    private readonly MotorViewModel _viewModel;

    /// <summary>
    /// 初始化电机调试窗口，并绑定视图模型及失败提示事件。
    /// </summary>
    public MotorDebugWindow()
        : this(new MotorViewModel())
    {
    }

    /// <summary>
    /// 使用主界面共享 PLC 连接初始化电机调试窗口。
    /// </summary>
    /// <param name="plcConnection">主界面共享 PLC 连接服务。</param>
    public MotorDebugWindow(SharedPlcConnectionService plcConnection)
        : this(new MotorViewModel(plcConnection.CreateClientLease(), plcConnection.EndpointText))
    {
        plcConnection.Disconnected += OnSharedPlcDisconnected;
        Unloaded += (_, _) => plcConnection.Disconnected -= OnSharedPlcDisconnected;

        Loaded += async (_, _) =>
        {
            bool started = await _viewModel.StartSharedPollingAsync().ConfigureAwait(true);
            if (!started)
            {
                MessageBox.Show(this, "请先在主界面连接 PLC。", "PLC未连接", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
    }

    private MotorDebugWindow(MotorViewModel viewModel)
    {
        _viewModel = viewModel;
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

    private void OnSharedPlcDisconnected(object? sender, System.EventArgs e)
    {
        Dispatcher.Invoke(() => _viewModel.StopPolling());
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
