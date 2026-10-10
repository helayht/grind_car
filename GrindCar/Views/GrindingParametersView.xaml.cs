using System.Windows;
using System.Windows.Controls;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>打磨参数页面；轮询及共享视图模型由主窗口管理。</summary>
public partial class GrindingParametersView : UserControl
{
    private readonly GrindingParametersViewModel _viewModel;

    public GrindingParametersView(GrindingParametersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => _viewModel.Save();
    private async void WriteAll_Click(object sender, RoutedEventArgs e) => await _viewModel.WriteAsync(false);
    private async void WriteAngle_Click(object sender, RoutedEventArgs e) => await _viewModel.WriteAsync(true);
}
