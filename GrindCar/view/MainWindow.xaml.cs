using System.Windows;
using GrindCar.viewModel;

namespace GrindCar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MotorViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.StartPollingAsync("127.0.0.1", 502, 1, 1000);
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.StopPolling();
        }
    }
}
