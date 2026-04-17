using System.Collections.Generic;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 代表轨面与标准轨面的曲线对比窗口。
/// </summary>
public partial class RepresentativeProfileComparisonWindow : Window
{
    private readonly RepresentativeProfileComparisonViewModel _viewModel;

    /// <summary>
    /// 初始化代表轨面与标准轨面对比窗口。
    /// </summary>
    /// <param name="representativePoints">待展示的代表截面点集。</param>
    public RepresentativeProfileComparisonWindow(IReadOnlyList<RailProfilePoint> representativePoints)
    {
        InitializeComponent();
        _viewModel = new RepresentativeProfileComparisonViewModel(representativePoints);
        DataContext = _viewModel;
        Loaded += RepresentativeProfileComparisonWindow_Loaded;
    }

    /// <summary>
    /// 窗口加载完成后绘制对比曲线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">窗口加载事件参数。</param>
    private void RepresentativeProfileComparisonWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdatePlot(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight);
    }

    /// <summary>
    /// 在绘图区域尺寸变化时重新渲染曲线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">尺寸变化事件参数。</param>
    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.UpdatePlot(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight);
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
}
