using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.ViewModels;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 打磨深度调试窗口。
/// </summary>
public partial class GrindDepthDebugWindow : Window
{
    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly GrindDepthDebugViewModel _viewModel = new();

    /// <summary>
    /// 初始化打磨深度调试窗口并绑定视图模型。
    /// </summary>
    public GrindDepthDebugWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>
    /// 解析输入角度并执行打磨深度计算，同时展示结果窗口。
    /// </summary>
    private async void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<RailProfilePoint> representativePoints = await _viewModel.CalculateAsync();
            if (representativePoints.Count == 0)
            {
                return;
            }

            var comparisonWindow = new RepresentativeProfileComparisonWindow(representativePoints)
            {
                Owner = this
            };
            comparisonWindow.Show();
        }
        catch (Exception ex)
        {
            _viewModel.SetErrorStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "计算失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 加载基线并检测机器已打磨深度。
    /// </summary>
    private async void Detect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.DetectAsync();
        }
        catch (Exception ex)
        {
            _viewModel.SetErrorStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "检测失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 关闭当前窗口。
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 导出最近一次打磨深度计算所使用的代表点坐标。
    /// </summary>
    private void ExportRepresentativePoints_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出代表点坐标",
            Filter = CsvFileFilter,
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"representative-points-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _viewModel.ExportRepresentativePoints(dialog.FileName);
            MessageBox.Show(this, "代表点导出完成。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _viewModel.SetErrorStatus($"导出代表点失败: {ex.Message}");
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}