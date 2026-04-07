using System.Windows;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 电机调试窗口。
/// </summary>
public partial class MotorDebugWindow : Window
{
    private readonly MotorViewModel _viewModel = new();

    public MotorDebugWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Unloaded += MotorDebugWindow_Unloaded;
        _viewModel.ConnectionFailed += OnConnectionFailed;
    }

    private void MotorDebugWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Shutdown();
    }

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
