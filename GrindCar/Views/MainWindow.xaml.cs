using System.Windows;
using GrindCar.ViewModels;

namespace GrindCar.Views
{
    /// <summary>
    /// 主窗口逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // ViewModel 实例
        private readonly MotorViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Unloaded += MainWindow_Unloaded;
        }

        /// <summary>
        /// 窗口卸载时停止轮询
        /// </summary>
        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Shutdown();
        }

        private void OpenMotorDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new MotorDebugWindow
            {
                Owner = this
            };
            window.Show();
        }

        private void OpenPointCloudExportWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new PointCloudExportWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void OpenMedianSectionDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new MedianSectionDebugWindow
            {
                Owner = this
            };
            window.Show();
        }

        private void OpenGrindDepthDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new GrindDepthDebugWindow
            {
                Owner = this
            };
            window.Show();
        }
    }
}
