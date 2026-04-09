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

        /// <summary>
        /// 初始化主窗口，并绑定驾驶舱视图模型。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Unloaded += MainWindow_Unloaded;
        }

        /// <summary>
        /// 窗口卸载时停止轮询
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">窗口卸载事件参数。</param>
        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Shutdown();
        }

        /// <summary>
        /// 打开电机调试窗口。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
        private void OpenMotorDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new MotorDebugWindow
            {
                Owner = this
            };
            window.Show();
        }

        /// <summary>
        /// 打开点云导出窗口。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
        private void OpenPointCloudExportWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new PointCloudExportWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        /// <summary>
        /// 打开中位截面调试窗口。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
        private void OpenMedianSectionDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new MedianSectionDebugWindow
            {
                Owner = this
            };
            window.Show();
        }

        /// <summary>
        /// 打开打磨深度调试窗口。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">按钮点击事件参数。</param>
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
