using System;
using System.Windows;
using GrindCar.Services.Rail;
using GrindCar.ViewModels;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 中位 Y 截面调试窗口。
/// </summary>
public partial class MedianSectionDebugWindow : Window
{
    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly MedianSectionDebugViewModel _viewModel = new();

    /// <summary>
    /// 初始化中位截面调试窗口并绑定当前窗口为数据上下文。
    /// </summary>
    public MedianSectionDebugWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>
    /// 选择 CSV 文件并异步提取中位截面点集。
    /// </summary>
    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择点云 CSV 文件",
            Filter = CsvFileFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string filePath = dialog.FileName;

        try
        {
            await _viewModel.ImportAsync(filePath);
        }
        catch (RepresentativeProfileExtractionException ex)
        {
            _viewModel.SetErrorStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "提取失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            const string message = "提取中位 Y 截面时发生未处理异常。";
            _viewModel.SetErrorStatus($"{message} {ex.Message}");
            MessageBox.Show(this, $"{message}\n{ex.Message}", "提取失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 将当前提取到的代表截面点导出为 CSV 文件。
    /// </summary>
    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出代表截面坐标",
            Filter = CsvFileFilter,
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = _viewModel.BuildDefaultExportFileName()
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _viewModel.Export(dialog.FileName);
            MessageBox.Show(this, "代表截面坐标导出完成。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            const string message = "导出代表截面坐标时发生异常。";
            _viewModel.SetErrorStatus($"{message} {ex.Message}");
            MessageBox.Show(this, $"{message}\n{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 关闭当前窗口。
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}