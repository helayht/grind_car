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
    private const string DefaultMeasurementPlcIpAddress = "192.168.1.10";
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
        try
        {
            var window = new PointCloudExportWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"打开点云导出窗口失败：{ex.Message}", "点云导出", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 打开代表截面调试窗口。
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
    /// 将主界面中的打磨起点和终点参数写入 PLC。
    /// </summary>
    private async void WriteGrindingParameters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Measurement.WriteGrindingParametersAsync();
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"写入失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "参数写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 保存主界面点云在线采集参数。
    /// </summary>
    private void SavePointCloudCaptureSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Measurement.SavePointCloudCaptureSettings();
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"点云采集参数保存失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "点云采集参数保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 启动测量运行流程，并在用户确认打磨深度后写回各角度打磨次数。
    /// </summary>
    private async void StartMeasurementMotion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MeasurementGrindingWorkflowResult result = await Measurement.StartMeasurementMotionAsync();
            var confirmationWindow = new GrindDepthConfirmationWindow(result.Results)
            {
                Owner = this
            };

            if (confirmationWindow.ShowDialog() != true)
            {
                Measurement.SetMeasurementWriteCanceled();
                MessageBox.Show(this, "已取消写入 PLC。", "流程取消", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await Measurement.WriteGrindingTimesAsync(confirmationWindow.ConfirmedResults);
            MessageBox.Show(this, "打磨次数已写入 PLC。", "流程完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"测量流程执行失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "测量流程失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 触发打磨运动启动信号写入 PLC。
    /// </summary>
    private async void StartGrindingMotion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Measurement.StartGrindingMotionAsync();
            MessageBox.Show(this, "打磨运动启动信号已写入 PLC。", "操作完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"打磨运动启动失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "打磨运动启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
