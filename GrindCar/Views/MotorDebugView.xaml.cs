using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GrindCar.Services;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>保留编辑状态，按页面可见性管理共享连接的轮询。</summary>
public partial class MotorDebugView : UserControl, IDisposable
{
    private readonly MotorViewModel _viewModel;
    private readonly SharedPlcConnectionService _connection;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private bool _active;
    private bool _disposed;

    public MotorDebugView(SharedPlcConnectionService connection)
    {
        _connection = connection;
        _viewModel = new MotorViewModel(connection.CreateClientLease(), connection.EndpointText);
        InitializeComponent();
        DataContext = _viewModel;
        _connection.PropertyChanged += Connection_PropertyChanged;
        _viewModel.ConnectionFailed += OnConnectionFailed;
    }

    public Task ActivateAsync()
    {
        _active = true;
        return RefreshPollingAsync();
    }

    public Task DeactivateAsync()
    {
        _active = false;
        return RefreshPollingAsync();
    }

    private async Task RefreshPollingAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            await _viewModel.StopPollingAsync();
            if (_active && !_disposed)
                await _viewModel.StartSharedPollingAsync(_connection.EndpointText);
        }
        finally { _lifecycleLock.Release(); }
    }

    private void Connection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SharedPlcConnectionService.IsConnected)) return;
        // 异步投递，避免通信线程持有共享连接锁时等待 UI。
        Dispatcher.InvokeAsync(async () =>
        {
            if (_disposed) return;
            try { await RefreshPollingAsync(); }
            catch (Exception ex) { OnConnectionFailed(ex.Message); }
        });
    }

    private void OnConnectionFailed(string message)
    {
        if (_disposed || string.IsNullOrWhiteSpace(message)) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
                MessageBox.Show(Window.GetWindow(this), message, "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _active = false;
        _connection.PropertyChanged -= Connection_PropertyChanged;
        _viewModel.ConnectionFailed -= OnConnectionFailed;
        _viewModel.Shutdown();
    }
}
