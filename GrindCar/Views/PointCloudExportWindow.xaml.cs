using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GrindCar.Models.PointCloud;
using GrindCar.Services.PointCloud;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 点云导出窗口。
/// </summary>
public partial class PointCloudExportWindow : Window
{
    private readonly ObservableCollection<PointCloudDeviceInfo> _devices = new();
    private readonly PointCloudExportService _pointCloudExportService = new();

    /// <summary>
    /// 初始化点云导出窗口并绑定设备列表。
    /// </summary>
    public PointCloudExportWindow()
    {
        InitializeComponent();
        DeviceComboBox.ItemsSource = _devices;
        Loaded += PointCloudExportWindow_Loaded;
    }

    /// <summary>
    /// 窗口加载后自动刷新设备列表。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">窗口加载事件参数。</param>
    private async void PointCloudExportWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

    /// <summary>
    /// 手动刷新可用点云设备列表。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

    /// <summary>
    /// 打开文件选择对话框，设置点云导出路径。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void ChooseOutputPath_Click(object sender, RoutedEventArgs e)
    {
        PointCloudExportFormat exportFormat = GetSelectedExportFormat();
        var dialog = new SaveFileDialog
        {
            Title = "选择点云导出路径",
            Filter = GetDialogFilter(exportFormat),
            DefaultExt = GetDefaultExtension(exportFormat),
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
            SetStatus($"已选择导出路径: {dialog.FileName}");
        }
    }

    /// <summary>
    /// 导出当前选中设备的单帧点云文件。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceComboBox.SelectedItem is not PointCloudDeviceInfo selectedDevice)
        {
            MessageBox.Show(this, "请先选择设备。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string outputPath = OutputPathTextBox.Text;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show(this, "请先选择导出路径。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string serialNumber = selectedDevice.SerialNumber;
        PointCloudExportFormat exportFormat = GetSelectedExportFormat();
        ToggleBusyState(true);
        SetStatus($"正在导出设备 {serialNumber} 的点云数据...");

        try
        {
            await Task.Run(() => _pointCloudExportService.ExportPointCloud(
                serialNumber,
                outputPath,
                exportFormat));

            SetStatus($"导出完成: {outputPath}");
            MessageBox.Show(this, "点云导出完成。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (PointCloudSdkException ex)
        {
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            const string message = "导出点云时发生未处理异常。";
            SetStatus($"{message} {ex.Message}");
            MessageBox.Show(this, $"{message}\n{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    /// <summary>
    /// 关闭当前窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 异步刷新当前可用点云设备列表。
    /// </summary>
    /// <returns>表示刷新操作的任务。</returns>
    private async Task RefreshDevicesAsync()
    {
        ToggleBusyState(true);
        SetStatus("正在刷新设备列表...");

        try
        {
            IReadOnlyList<PointCloudDeviceInfo> devices = await Task.Run(() => _pointCloudExportService.GetDevices());
            _devices.Clear();
            foreach (PointCloudDeviceInfo device in devices)
            {
                _devices.Add(device);
            }

            DeviceComboBox.SelectedItem = _devices.FirstOrDefault();
            SetStatus(_devices.Count == 0 ? "未发现可用设备。" : $"已发现 {_devices.Count} 台设备。");
        }
        catch (PointCloudSdkException ex)
        {
            _devices.Clear();
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "刷新设备失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    /// <summary>
    /// 根据界面选择项解析当前导出格式。
    /// </summary>
    /// <returns>选中的点云导出格式；若未选择则默认返回 <see cref="PointCloudExportFormat.Ply"/>。</returns>
    private PointCloudExportFormat GetSelectedExportFormat()
    {
        if (FormatComboBox.SelectedItem is not ComboBoxItem selectedItem)
        {
            return PointCloudExportFormat.Ply;
        }

        return selectedItem.Tag?.ToString() switch
        {
            "Csv" => PointCloudExportFormat.Csv,
            "Obj" => PointCloudExportFormat.Obj,
            _ => PointCloudExportFormat.Ply
        };
    }

    /// <summary>
    /// 生成保存文件对话框使用的过滤器文本。
    /// </summary>
    /// <param name="exportFormat">目标导出格式。</param>
    /// <returns>与导出格式匹配的过滤器字符串。</returns>
    private static string GetDialogFilter(PointCloudExportFormat exportFormat)
    {
        return exportFormat switch
        {
            PointCloudExportFormat.Csv => "CSV 文件|*.csv",
            PointCloudExportFormat.Obj => "OBJ 文件|*.obj",
            _ => "PLY 文件|*.ply"
        };
    }

    /// <summary>
    /// 获取指定导出格式对应的默认文件扩展名。
    /// </summary>
    /// <param name="exportFormat">目标导出格式。</param>
    /// <returns>对应的默认扩展名。</returns>
    private static string GetDefaultExtension(PointCloudExportFormat exportFormat)
    {
        return exportFormat switch
        {
            PointCloudExportFormat.Csv => ".csv",
            PointCloudExportFormat.Obj => ".obj",
            _ => ".ply"
        };
    }

    /// <summary>
    /// 统一切换窗口中与导出相关控件的忙碌状态。
    /// </summary>
    /// <param name="isBusy">是否处于忙碌状态。</param>
    private void ToggleBusyState(bool isBusy)
    {
        DeviceComboBox.IsEnabled = !isBusy;
        FormatComboBox.IsEnabled = !isBusy;
        ExportButton.IsEnabled = !isBusy;
        OutputPathTextBox.IsEnabled = !isBusy;
    }

    /// <summary>
    /// 更新窗口底部状态提示文本。
    /// </summary>
    /// <param name="message">要显示的状态消息。</param>
    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}
