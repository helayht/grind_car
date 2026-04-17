using System;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Services.PointCloud;
using GrindCar.ViewModels;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 点云导出窗口。
/// </summary>
public partial class PointCloudExportWindow : Window
{
    private readonly PointCloudExportViewModel _viewModel = new();

    /// <summary>
    /// 初始化点云导出窗口并绑定设备列表。
    /// </summary>
    public PointCloudExportWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
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
        var dialog = new SaveFileDialog
        {
            Title = "选择点云导出路径",
            Filter = _viewModel.GetDialogFilter(),
            DefaultExt = _viewModel.GetDefaultExtension(),
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SetOutputPath(dialog.FileName);
        }
    }

    /// <summary>
    /// 导出当前选中设备的单帧点云文件。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ExportAsync();
            MessageBox.Show(this, "点云导出完成。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (PointCloudSdkException ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            const string message = "导出点云时发生未处理异常。";
            MessageBox.Show(this, $"{message}\n{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
        try
        {
            await _viewModel.RefreshDevicesAsync();
        }
        catch (PointCloudSdkException ex)
        {
            MessageBox.Show(this, ex.Message, "刷新设备失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"刷新设备时发生未处理异常：{ex.Message}", "刷新设备失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
