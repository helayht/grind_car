using System;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using GrindCar.Services;
using GrindCar.Services.Measurement;
using GrindCar.Services.Grinding;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 主窗口逻辑。
/// </summary>
public partial class MainWindow : Window
{
    private const string DefaultMeasurementPlcIpAddress = "192.168.1.10";
    private const int DefaultMeasurementPlcPort = 502;
    private const byte DefaultPlcUnitId = 1;

    private readonly MotorViewModel _viewModel = new();
    private readonly SharedPlcConnectionService _plcConnection;
    private readonly MainWindowMeasurementViewModel _measurementViewModel;
    private readonly GrindingParametersViewModel _grindingViewModel;
    private bool _shutdownReady;
    private bool _shutdownPending;

    public MainWindowMeasurementViewModel Measurement => _measurementViewModel;

    /// <summary>
    /// 初始化主窗口，并绑定驾驶舱视图模型。
    /// </summary>
    public MainWindow()
    {
        _plcConnection = new SharedPlcConnectionService(
            DefaultMeasurementPlcIpAddress,
            DefaultMeasurementPlcPort,
            DefaultPlcUnitId);
        _measurementViewModel = new MainWindowMeasurementViewModel(
            new MeasurementParameterService((ipAddress, port) => _plcConnection.CreateClientLease()),
            _plcConnection,
            DefaultMeasurementPlcIpAddress,
            DefaultMeasurementPlcPort);

        _grindingViewModel = new GrindingParametersViewModel(
            new GrindingParameterService(() => _plcConnection.CreateClientLease()),
            new GrindingSettingsStore(), _plcConnection, _measurementViewModel);
        InitializeComponent();
        Closing += async (_, e) =>
        {
            if (_shutdownReady) return;
            e.Cancel = true;
            if (_navigating || _grindingViewModel.IsBusy || !Measurement.CanOperate)
            {
                MessageBox.Show(this, "当前操作尚未完成，请等待结束后退出。", "操作进行中");
                return;
            }
            if (_shutdownPending) return;
            _shutdownPending = true;
            NavigationButtons.IsEnabled = false;
            PageArea.IsEnabled = false;
            try
            {
                if (_motorView != null) await _motorView.DeactivateAsync();
                await _grindingViewModel.StopPollingAsync();
                await Measurement.Telemetry.StopPollingAsync();
                _shutdownReady = true;
                await Dispatcher.InvokeAsync(Close);
            }
            catch (Exception ex)
            {
                _shutdownPending = false;
                NavigationButtons.IsEnabled = true;
                PageArea.IsEnabled = true;
                MessageBox.Show(this, $"停止后台任务失败：{ex.Message}", "退出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        DataContext = _viewModel;
        Loaded += (_, _) => Measurement.Telemetry.StartPolling();
        GrindCar.Infrastructure.WindowLayout.FitToWorkArea(this);
        _measurementViewModel.MeasurementEnded += MeasurementViewModel_MeasurementEnded;
        Closed += MainWindow_Closed;
    }

    /// <summary>
    /// 窗口卸载时停止轮询。
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _measurementViewModel.MeasurementEnded -= MeasurementViewModel_MeasurementEnded;
        if (_validationView != null) _validationView.ReturnHomeRequested -= ReturnHomeRequested;
        if (_registrationView != null) _registrationView.ReturnHomeRequested -= ReturnHomeRequested;
        _motorView?.Dispose();
        _viewModel.Shutdown();
        _grindingViewModel.Dispose();
        _measurementViewModel.Dispose();
        _plcConnection.Dispose();
    }

    private void MeasurementViewModel_MeasurementEnded()
    {
        MessageBox.Show(this, "测量结束。", "测量流程", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 切换暗色/亮色主题。
    /// </summary>
    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.ToggleTheme();

        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = ThemeService.CurrentTheme == ThemeType.Dark ? "☀" : "🌙";
        }
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
    /// 打开 PLC 连接设置窗口并应用配置。
    /// </summary>
    private async void OpenPlcConnectionSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new PlcConnectionSettingsWindow(Measurement.PlcIpAddress, Measurement.PlcPort)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            try
            {
                await Measurement.ConnectPlcAsync(window.SelectedIpAddress, window.SelectedPort);
            }
            catch (Exception ex)
            {
                Measurement.SetErrorStatus($"PLC连接失败：{ex.Message}");
                MessageBox.Show(this, ex.Message, "PLC连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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

    private void SaveMeasurementPositions_Click(object sender, RoutedEventArgs e) => Measurement.SaveMeasurementPositions();


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
    private void ShowMeasurementProfileComparison(Window owner, MeasurementGrindingWorkflowResult result)
    {
        RepresentativeProfileComparisonWindow? window = null;
        try
        {
            window = new RepresentativeProfileComparisonWindow(result.RepresentativeProfiles) { Owner = owner };
            if (window.HasDrawableProfiles)
                window.ShowDialog();
            else
                MessageBox.Show(owner, "本次测量没有可绘制的代表廓形，将继续确认打磨深度。",
                    "曲线对比", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"曲线对比无法显示：{ex.Message}\n将继续确认打磨深度。",
                "曲线对比", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            window?.Close();
        }
    }

    /// <summary>正式测量和 CSV 验证共用流程锁及后处理入口。</summary>
    private async Task RunResultWorkflowAsync(
        Window owner, Func<Task<MeasurementGrindingWorkflowResult>> calculateAsync, bool offline = false)
    {
        if (!Measurement.CanOperate)
        {
            MessageBox.Show(owner, "当前操作尚未完成，请等待结束后重试。", "操作进行中");
            return;
        }

        var otherWindows = OwnedWindows.Cast<Window>()
            .Where(window => window != owner && window.IsEnabled).ToArray();
        _grindingViewModel.SetMeasurementActive(true);
        Measurement.SetWorkflowActive(true);
        try
        {
            foreach (Window window in otherWindows)
                window.SetCurrentValue(IsEnabledProperty, false);
            if (offline) Measurement.Activity.Begin(offline: true);
            MeasurementGrindingWorkflowResult result = await calculateAsync();
            await ProcessGrindingResultAsync(owner, result);
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"流程执行失败：{ex.Message}");
            throw;
        }
        finally
        {
            foreach (Window window in otherWindows)
                window.SetCurrentValue(IsEnabledProperty, true);
            _grindingViewModel.SetMeasurementActive(false);
            Measurement.SetWorkflowActive(false);
        }
    }

    /// <summary>先查看曲线，再确认深度并写回次数；后处理错误在此统一报告。</summary>
    private async Task ProcessGrindingResultAsync(Window owner, MeasurementGrindingWorkflowResult result)
    {
        try
        {
            Measurement.Activity.SetStage(GrindCar.Models.Measurement.MeasurementWorkflowStage.Confirming);
            ShowMeasurementProfileComparison(owner, result);
            var confirmationWindow = new GrindDepthConfirmationWindow(result.Results, result.MaximumDropProfile)
            {
                Owner = owner
            };
            if (confirmationWindow.ShowDialog() != true)
            {
                Measurement.SetMeasurementWriteCanceled();
                MessageBox.Show(owner, "已取消写入 PLC。", "流程取消", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _grindingViewModel.SetConfirmedResults(confirmationWindow.ConfirmedResults, "待写入");
            try
            {
                await Measurement.WriteGrindingTimesAsync(confirmationWindow.ConfirmedResults);
                _grindingViewModel.SetConfirmedResults(confirmationWindow.ConfirmedResults, "写入成功");
            }
            catch (Exception ex)
            {
                _grindingViewModel.SetConfirmedResults(confirmationWindow.ConfirmedResults, "写入失败/可能部分写入");
                Measurement.SetErrorStatus($"打磨次数写入失败：{ex.Message}");
                MessageBox.Show(owner, ex.Message, "打磨次数写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show(owner, "打磨次数已写入 PLC。", "流程完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Measurement.SetErrorStatus($"结果处理失败：{ex.Message}");
            MessageBox.Show(owner, ex.Message, "结果处理失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StartMeasurementMotion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunResultWorkflowAsync(this, Measurement.StartMeasurementMotionAsync);
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
