using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Definitions;
using GrindCar.Services;
using GrindCar.ViewModels;

namespace GrindCar.Views
{
    /// <summary>
    /// 主窗口逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private const byte DefaultUnitId = 1;
        private const string DefaultMeasurementPlcIpAddress = "127.0.0.1";
        private const int DefaultMeasurementPlcPort = 502;

        // ViewModel 实例
        private readonly MotorViewModel _viewModel = new();
        private string _measurementPlcIpAddress = DefaultMeasurementPlcIpAddress;
        private int _measurementPlcPort = DefaultMeasurementPlcPort;

        /// <summary>
        /// 初始化主窗口，并绑定驾驶舱视图模型。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Unloaded += MainWindow_Unloaded;
            MeasurementPlcEndpointTextBlock.Text = $"{_measurementPlcIpAddress}:{_measurementPlcPort}";
        }

        /// <summary>
        /// 窗口卸载时停止轮询
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">窗口卸载事件参数。</param>
        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Shutdown();
        }

        /// <summary>
        /// 打开电机调试窗口。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
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
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
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
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
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
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
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
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
        private void OpenPlcConnectionSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new PlcConnectionSettingsWindow(_measurementPlcIpAddress, _measurementPlcPort)
            {
                Owner = this
            };

            if (window.ShowDialog() == true)
            {
                _measurementPlcIpAddress = window.SelectedIpAddress;
                _measurementPlcPort = window.SelectedPort;
                MeasurementPlcEndpointTextBlock.Text = $"{_measurementPlcIpAddress}:{_measurementPlcPort}";
                MeasurementParameterStatusTextBlock.Text = $"PLC连接参数已更新：{_measurementPlcIpAddress}:{_measurementPlcPort}";
            }
        }

        /// <summary>
        /// 打开打磨深度调试窗口。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
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
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
        private async void WriteMeasurementParameters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleMeasurementWriteBusyState(true);
                string ipAddress = _measurementPlcIpAddress;
                int port = _measurementPlcPort;
                double startPosition = ParsePosition(MeasurementStartPositionTextBox.Text, "测量起点位置");
                double endPosition = ParsePosition(MeasurementEndPositionTextBox.Text, "测量终点位置");

                if (startPosition > endPosition)
                {
                    throw new InvalidOperationException("测量起点位置不能大于测量终点位置。");
                }

                int startRawValue = ToScaledInt32(
                    startPosition,
                    MotorParameterDefinitions.MeasurementStartPositionScale,
                    MotorParameterDefinitions.MeasurementStartPositionName);
                int endRawValue = ToScaledInt32(
                    endPosition,
                    MotorParameterDefinitions.MeasurementEndPositionScale,
                    MotorParameterDefinitions.MeasurementEndPositionName);

                MeasurementParameterStatusTextBlock.Text = "正在写入测量参数...";
                await Task.Run(async () =>
                {
                    using IPlcClient plcClient = new PlcModbusCommunicator(ipAddress, port, DefaultUnitId);
                    await plcClient.ConnectAsync().ConfigureAwait(false);
                    plcClient.WriteInt32(MotorParameterDefinitions.MeasurementStartPositionAddress, startRawValue);
                    plcClient.WriteInt32(MotorParameterDefinitions.MeasurementEndPositionAddress, endRawValue);
                    plcClient.Disconnect();
                });

                MeasurementParameterStatusTextBlock.Text =
                    $"写入成功：起点 {startPosition:0.###}m，终点 {endPosition:0.###}m";
            }
            catch (Exception ex)
            {
                MeasurementParameterStatusTextBlock.Text = $"写入失败：{ex.Message}";
                MessageBox.Show(this, ex.Message, "参数写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ToggleMeasurementWriteBusyState(false);
            }
        }

        /// <summary>
        /// 向 PLC 发送测量运动启动信号（M31 置位）。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
        private async void StartMeasurementMotion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleMeasurementWriteBusyState(true);
                string ipAddress = _measurementPlcIpAddress;
                int port = _measurementPlcPort;

                MeasurementParameterStatusTextBlock.Text = "正在发送测量运动启动信号...";
                await Task.Run(async () =>
                {
                    using IPlcClient plcClient = new PlcModbusCommunicator(ipAddress, port, DefaultUnitId);
                    await plcClient.ConnectAsync().ConfigureAwait(false);
                    await plcClient.WriteSingleCoilAsync(MotorParameterDefinitions.MeasurementMotionStartAddress, true)
                        .ConfigureAwait(false);
                    plcClient.Disconnect();
                });

                const string successMessage = "测量运动启动信号已发送。";
                MeasurementParameterStatusTextBlock.Text = successMessage;
                MessageBox.Show(this, successMessage, "启动成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MeasurementParameterStatusTextBlock.Text = $"测量运动启动失败：{ex.Message}";
                MessageBox.Show(this, ex.Message, "测量运动启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ToggleMeasurementWriteBusyState(false);
            }
        }

        /// <summary>
        /// 切换主界面参数写入控件的忙碌状态。
        /// </summary>
        /// <param name="isBusy">是否忙碌。</param>
        private void ToggleMeasurementWriteBusyState(bool isBusy)
        {
            WriteMeasurementParametersButton.IsEnabled = !isBusy;
            StartMeasurementMotionButton.IsEnabled = !isBusy;
            OpenPlcConnectionSettingsButton.IsEnabled = !isBusy;
            MeasurementStartPositionTextBox.IsEnabled = !isBusy;
            MeasurementEndPositionTextBox.IsEnabled = !isBusy;
        }

        /// <summary>
        /// 解析测量位置输入值。
        /// </summary>
        /// <param name="rawValue">输入文本。</param>
        /// <param name="name">参数名称。</param>
        /// <returns>解析后的数值（m）。</returns>
        private static double ParsePosition(string rawValue, string name)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new InvalidOperationException($"{name}不能为空。");
            }

            if (double.TryParse(rawValue.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
            {
                return value;
            }

            if (double.TryParse(rawValue.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            throw new InvalidOperationException($"{name}格式无效。");
        }

        /// <summary>
        /// 将工程量按比例转换为 PLC 原始 Int32 值。
        /// </summary>
        /// <param name="value">输入工程量。</param>
        /// <param name="scale">比例。</param>
        /// <param name="name">参数名称。</param>
        /// <returns>可写入 PLC 的原始值。</returns>
        private static int ToScaledInt32(double value, double scale, string name)
        {
            double scaled = value * scale;
            if (scaled > int.MaxValue || scaled < int.MinValue)
            {
                throw new InvalidOperationException($"{name}超出 Int32 范围。");
            }

            return (int)Math.Round(scaled);
        }
    }
}
