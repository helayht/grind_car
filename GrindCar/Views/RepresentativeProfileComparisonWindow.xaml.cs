using System.Collections.Generic;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
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
    public RepresentativeProfileComparisonWindow(
        IReadOnlyList<RailProfilePoint> representativePoints,
        RepresentativeProfileCurveStyle curveStyle = RepresentativeProfileCurveStyle.Smooth)
        : this(
            new IReadOnlyList<RailProfilePoint>[] { representativePoints },
            curveStyle)
    {
    }

    /// <summary>
    /// 初始化包含多个独立片段的代表轨面与标准轨面对比窗口。
    /// </summary>
    public RepresentativeProfileComparisonWindow(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> representativeSegments,
        RepresentativeProfileCurveStyle curveStyle)
    {
        InitializeComponent();
        _viewModel = new RepresentativeProfileComparisonViewModel(representativeSegments, curveStyle);
        DataContext = _viewModel;
        Loaded += RepresentativeProfileComparisonWindow_Loaded;
    }

    /// <summary>
    /// 初始化 Left/Right 最大掉块廓形同图对比窗口。
    /// </summary>
    public RepresentativeProfileComparisonWindow(
        MaximumDropProfileResult? leftMaximumDropProfile,
        MaximumDropProfileResult? rightMaximumDropProfile)
    {
        InitializeComponent();
        Title = "Left/Right 最大掉块廓形与标准轨面对比";
        _viewModel = new RepresentativeProfileComparisonViewModel(
            leftMaximumDropProfile,
            rightMaximumDropProfile);
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
