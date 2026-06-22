using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GrindCar.Services;

/// <summary>
/// 维护主界面共享 PLC 连接，并为不同业务入口提供串行化读写代理。
/// </summary>
public sealed class SharedPlcConnectionService : INotifyPropertyChanged, IDisposable
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly byte _unitId;
    private IPlcClient? _client;
    private string _ipAddress;
    private int _port;
    private string _connectionStatus = "未连接";

    public SharedPlcConnectionService(string defaultIpAddress, int defaultPort, byte unitId)
    {
        _ipAddress = defaultIpAddress;
        _port = defaultPort;
        _unitId = unitId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? Disconnected;

    public string IpAddress => _ipAddress;

    public int Port => _port;

    public string EndpointText => $"{_ipAddress}:{_port}";

    public bool IsConnected => _client?.IsConnected == true;

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (_connectionStatus == value)
            {
                return;
            }

            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    public async Task ConnectAsync(string ipAddress, int port)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new InvalidOperationException("PLC IP 不能为空。");
        }

        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client != null)
            {
                _client.Disconnect();
                _client.Dispose();
                _client = null;
            }

            _ipAddress = ipAddress.Trim();
            _port = port;
            NotifyEndpointChanged();
            ConnectionStatus = $"连接中 {_ipAddress}:{_port}";

            var client = new PlcModbusCommunicator(_ipAddress, _port, _unitId);
            try
            {
                await client.ConnectAsync().ConfigureAwait(false);
            }
            catch
            {
                client.Dispose();
                ConnectionStatus = "未连接";
                OnPropertyChanged(nameof(IsConnected));
                throw;
            }

            _client = client;
            ConnectionStatus = $"已连接 {_ipAddress}:{_port}";
            OnPropertyChanged(nameof(IsConnected));
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public IPlcClient CreateClientLease()
    {
        return new SharedPlcClientLease(this);
    }

    public void Disconnect()
    {
        _operationLock.Wait();
        try
        {
            DisconnectCore();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Dispose()
    {
        Disconnect();
        _operationLock.Dispose();
    }

    private IPlcClient GetConnectedClient()
    {
        if (_client?.IsConnected != true)
        {
            throw new InvalidOperationException("请先在主界面连接 PLC。");
        }

        return _client;
    }

    private T Execute<T>(Func<IPlcClient, T> operation)
    {
        _operationLock.Wait();
        try
        {
            return operation(GetConnectedClient());
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void Execute(Action<IPlcClient> operation)
    {
        _operationLock.Wait();
        try
        {
            operation(GetConnectedClient());
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task ExecuteAsync(Func<IPlcClient, Task> operation)
    {
        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await operation(GetConnectedClient()).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void DisconnectCore()
    {
        if (_client != null)
        {
            _client.Disconnect();
            _client.Dispose();
            _client = null;
        }

        ConnectionStatus = "未连接";
        OnPropertyChanged(nameof(IsConnected));
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyEndpointChanged()
    {
        OnPropertyChanged(nameof(IpAddress));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(EndpointText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class SharedPlcClientLease : IPlcClient
    {
        private readonly SharedPlcConnectionService _owner;

        public SharedPlcClientLease(SharedPlcConnectionService owner)
        {
            _owner = owner;
        }

        public bool IsConnected => _owner.IsConnected;

        public Task ConnectAsync()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("请先在主界面连接 PLC。");
            }

            return Task.CompletedTask;
        }

        public void Disconnect()
        {
        }

        public int ReadInt32(ushort startAddress)
        {
            return _owner.Execute(client => client.ReadInt32(startAddress));
        }

        public bool ReadSingleCoil(ushort coilAddress)
        {
            return _owner.Execute(client => client.ReadSingleCoil(coilAddress));
        }

        public void WriteInt32(ushort startAddress, int value)
        {
            _owner.Execute(client => client.WriteInt32(startAddress, value));
        }

        public void WriteInt16(ushort address, short value)
        {
            _owner.Execute(client => client.WriteInt16(address, value));
        }

        public Task WriteSingleCoilAsync(ushort coilAddress, bool value)
        {
            return _owner.ExecuteAsync(client => client.WriteSingleCoilAsync(coilAddress, value));
        }

        public void Dispose()
        {
        }
    }
}
