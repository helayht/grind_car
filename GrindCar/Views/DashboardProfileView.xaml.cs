using System.Windows;
using System.Windows.Controls;
using GrindCar.ViewModels;

namespace GrindCar.Views;

public partial class DashboardProfileView : UserControl
{
    public DashboardProfileView() => InitializeComponent();
    private void View_Loaded(object sender, RoutedEventArgs e) => UpdatePlotSize();
    private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdatePlotSize();
    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePlotSize();
    private void UpdatePlotSize()
    {
        if (PlotCanvas != null && DataContext is DashboardProfileViewModel viewModel)
            viewModel.Resize(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight);
    }
}
