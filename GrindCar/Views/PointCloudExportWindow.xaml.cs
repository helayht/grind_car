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

    public PointCloudExportWindow()
    {
        InitializeComponent();
        DeviceComboBox.ItemsSource = _devices;
        Loaded += PointCloudExportWindow_Loaded;
    }

    private async void PointCloudExportWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

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

    private static string GetDialogFilter(PointCloudExportFormat exportFormat)
    {
        return exportFormat switch
        {
            PointCloudExportFormat.Csv => "CSV 文件|*.csv",
            PointCloudExportFormat.Obj => "OBJ 文件|*.obj",
            _ => "PLY 文件|*.ply"
        };
    }

    private static string GetDefaultExtension(PointCloudExportFormat exportFormat)
    {
        return exportFormat switch
        {
            PointCloudExportFormat.Csv => ".csv",
            PointCloudExportFormat.Obj => ".obj",
            _ => ".ply"
        };
    }

    private void ToggleBusyState(bool isBusy)
    {
        DeviceComboBox.IsEnabled = !isBusy;
        FormatComboBox.IsEnabled = !isBusy;
        ExportButton.IsEnabled = !isBusy;
        OutputPathTextBox.IsEnabled = !isBusy;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}
