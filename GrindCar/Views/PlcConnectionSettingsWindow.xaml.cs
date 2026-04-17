using System.Globalization;
using System.Windows;

namespace GrindCar.Views;

/// <summary>
/// PLC 连接参数设置窗口。
/// </summary>
public partial class PlcConnectionSettingsWindow : Window
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;

    /// <summary>
    /// 初始化连接参数设置窗口。
    /// </summary>
    /// <param name="ipAddress">默认 IP 地址。</param>
    /// <param name="port">默认端口。</param>
    public PlcConnectionSettingsWindow(string ipAddress, int port)
    {
        InitializeComponent();
        IpAddressTextBox.Text = ipAddress;
        PortTextBox.Text = port.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 获取确认后的 IP 地址。
    /// </summary>
    public string SelectedIpAddress { get; private set; } = string.Empty;

    /// <summary>
    /// 获取确认后的端口号。
    /// </summary>
    public int SelectedPort { get; private set; }

    /// <summary>
    /// 确认并关闭窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮事件参数。</param>
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        string ipAddress = IpAddressTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            MessageBox.Show(this, "IP 不能为空。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) &&
            !int.TryParse(PortTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out port))
        {
            MessageBox.Show(this, "端口格式无效。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (port < MinPort || port > MaxPort)
        {
            MessageBox.Show(this, $"端口范围无效，请输入 {MinPort}~{MaxPort}。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedIpAddress = ipAddress;
        SelectedPort = port;
        DialogResult = true;
    }

    /// <summary>
    /// 取消并关闭窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮事件参数。</param>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
