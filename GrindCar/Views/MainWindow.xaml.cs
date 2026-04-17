using System;
using System.Windows;
using GrindCar.Services.Measurement;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 主窗口逻辑。
/// </summary>
public partial class MainWindow : Window
{
    private const string DefaultMeasurementPlcIpAddress = "127.0.0.1";
    private const int DefaultMeasurementPlcPort = 502;

    private readonly MotorViewModel _viewModel = new();
    private readonly MainWindowMeasurementViewModel _measurementViewModel =
        new(new MeasurementParameterService(), DefaultMeasurementPlcIpAddress, DefaultMeasurementPlcPort);

    public MainWindowMeasurementViewModel Measurement => _measurementViewModel;

    /// <summary>
    /// 初始化主窗口，并绑定驾驶舱视图模型。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Unloaded += MainWindow_Unloaded;
    }

    /// <summary>
    /// 窗口卸载时停止轮询。
    /// </summary>
    private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Shutdown();
    }

    /// <summary>
    /// 打开电机调试窗口。
    /// </summary>
    private void OpenMotorDebugWindow_Click(object sender, RoutedEventArgs e)
    {
        var window = new MotorDebugWindow
        {
            Owner = this
        };
        window.Show();
    }

    /// <summary>
    /// 打开点云导出窗口。
    /// </summary>
    private void OpenPointCloudExportWindow_Click(object sender, RoutedEventArgs e)
    {
        var window = new PointCloudExportWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }

    /// <summary>
    /// 打开中位截面调试窗口。
    /// </summary>
    private void OpenMedianSectionDebugWindow_Click(object sender, RoutedEventArgs e)
    {
        var window = new MedianSectionDebugWindow
        {
            Owner = this
        };
        window.Show();
    }

    /// <summary>
    /// 打开曲线旋转调试窗口。
    /// </summary>
    private void OpenCurveRotationDebugWindow_Click(object sender, RoutedEventArgs e)
    {
        var window = new CurveRotationDebugWindow
        {
            Owner = this
        };
        window.Show();
    }

    /// <summary>
    /// 打开 PLC 连接设置窗口并应用配置。
    /// </summary>
    private void OpenPlcConnectionSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new PlcConnectionSettingsWindow(Measurement.PlcIpAddress, Measurement.PlcPort)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            Measurement.UpdateEndpoint(window.SelectedIpAddress, window.SelectedPort);
        }
    }

    /// <summary>
    /// 打开打磨深度调试窗口。
    /// </summary>
    private void OpenGrindDepthDebugWindow_Click(object sender, RoutedEventArgs e)
    {
        var window = new GrindDepthDebugWindow
        {
            Owner = this
        };
        window.Show();
    }

    /// <summary>
    /// 将主界面中的测量起点和终点参数写入 PLC。
    /// </summary>
    private async void WriteMeasurementParameters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Measurement.WriteMeasurementParametersAsync();
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"写入失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "参数写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 向 PLC 发送测量运动启动信号（M31 置位）。
    /// </summary>
    private async void StartMeasurementMotion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Measurement.StartMeasurementMotionAsync();
            MessageBox.Show(this, "测量运动启动信号已发送。", "启动成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"测量运动启动失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "测量运动启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
