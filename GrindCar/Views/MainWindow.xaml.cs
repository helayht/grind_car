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
            _viewModel.ConnectionFailed += OnConnectionFailed;
        }

        /// <summary>
        /// 窗口卸载时停止轮询
        /// </summary>
        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.StopPolling();
        }

        private void OnConnectionFailed(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, message, "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }
}

