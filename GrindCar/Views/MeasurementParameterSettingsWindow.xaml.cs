using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Definitions;
using GrindCar.Services;

namespace GrindCar.Views;

/// <summary>
/// 测量起点/终点参数设置窗口。
/// </summary>
public partial class MeasurementParameterSettingsWindow : Window
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;
    private const byte DefaultUnitId = 1;

    /// <summary>
    /// 初始化参数设置窗口。
    /// </summary>
    public MeasurementParameterSettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 写入测量起点与终点参数到 PLC。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private async void WriteParameters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ToggleBusyState(true);
            string ipAddress = ValidateIpAddress(IpAddressTextBox.Text);
            int port = ValidatePort(PortTextBox.Text);
            double startPosition = ParsePosition(MeasurementStartTextBox.Text, "测量起点位置");
            double endPosition = ParsePosition(MeasurementEndTextBox.Text, "测量终点位置");

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

            StatusTextBlock.Text = "正在写入测量参数...";
            await Task.Run(async () =>
            {
                using IPlcClient plcClient = new PlcModbusCommunicator(ipAddress, port, DefaultUnitId);
                await plcClient.ConnectAsync().ConfigureAwait(false);
                plcClient.WriteInt32(MotorParameterDefinitions.MeasurementStartPositionAddress, startRawValue);
                plcClient.WriteInt32(MotorParameterDefinitions.MeasurementEndPositionAddress, endRawValue);
                plcClient.Disconnect();
            });

            StatusTextBlock.Text =
                $"写入成功：{MotorParameterDefinitions.MeasurementStartPositionName}={startPosition:0.###}m，{MotorParameterDefinitions.MeasurementEndPositionName}={endPosition:0.###}m";
            MessageBox.Show(this, "参数写入成功。", "写入成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"写入失败：{ex.Message}";
            MessageBox.Show(this, ex.Message, "写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    /// <summary>
    /// 关闭窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 切换窗口忙碌状态。
    /// </summary>
    /// <param name="isBusy">是否忙碌。</param>
    private void ToggleBusyState(bool isBusy)
    {
        WriteButton.IsEnabled = !isBusy;
        IpAddressTextBox.IsEnabled = !isBusy;
        PortTextBox.IsEnabled = !isBusy;
        MeasurementStartTextBox.IsEnabled = !isBusy;
        MeasurementEndTextBox.IsEnabled = !isBusy;
    }

    /// <summary>
    /// 校验并返回 IP 地址字符串。
    /// </summary>
    /// <param name="rawIpAddress">原始输入。</param>
    /// <returns>有效的 IP 地址文本。</returns>
    private static string ValidateIpAddress(string rawIpAddress)
    {
        string ipAddress = rawIpAddress.Trim();
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new InvalidOperationException("PLC IP 不能为空。");
        }

        return ipAddress;
    }

    /// <summary>
    /// 校验并解析端口号。
    /// </summary>
    /// <param name="rawPort">原始输入。</param>
    /// <returns>有效端口号。</returns>
    private static int ValidatePort(string rawPort)
    {
        if (!int.TryParse(rawPort.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) &&
            !int.TryParse(rawPort.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out port))
        {
            throw new InvalidOperationException("端口格式无效。");
        }

        if (port < MinPort || port > MaxPort)
        {
            throw new InvalidOperationException($"端口范围无效，请输入 {MinPort}~{MaxPort}。");
        }

        return port;
    }

    /// <summary>
    /// 解析测量位置输入值。
    /// </summary>
    /// <param name="rawValue">原始文本。</param>
    /// <param name="name">参数名称。</param>
    /// <returns>解析后的数值（单位 m）。</returns>
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
    /// 按比例将双精度值转换为 PLC 原始 Int32 值。
    /// </summary>
    /// <param name="value">输入值。</param>
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
