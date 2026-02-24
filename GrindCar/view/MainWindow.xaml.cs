using System.Windows;
using GrindCar.viewModel;

namespace GrindCar
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
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;
        }

        /// <summary>
        /// 窗口加载后启动 PLC 连接与轮询
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.StartPollingAsync("127.0.0.1", 502, 1, 1000);
        }

        /// <summary>
        /// 窗口卸载时停止轮询
        /// </summary>
        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.StopPolling();
        }
    }
}
