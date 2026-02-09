using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Model;
using GrindCar.tool;

namespace GrindCar.viewModel;

public class MotorViewModel : INotifyPropertyChanged
{
    private readonly Motor _parameters = new();
    private readonly ObservableCollection<MotorParameterItem> _items = new();
    private readonly MotorParameterItem _carCurrentPosition;
    private readonly MotorParameterItem _wheelLongitudinalCurrentPosition;
    private readonly MotorParameterItem _wheelLateralCurrentPosition;
    private readonly MotorParameterItem _wheelAngleCurrentPosition;
    private readonly MotorParameterItem _profilerCurrentPosition;

    private CancellationTokenSource _pollingCts;
    private Task _pollingTask;
    private PlcModbusCommunicator _plc;
    private readonly SynchronizationContext _uiContext;
    private string _connectionStatus = "未连接";

    public MotorViewModel()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        // 只读参数置前
        _carCurrentPosition = AddReadOnly("小车当前位置");
        _wheelLongitudinalCurrentPosition = AddReadOnly("砂轮纵向当前位置");
        _wheelLateralCurrentPosition = AddReadOnly("砂轮横向当前位置");
        _wheelAngleCurrentPosition = AddReadOnly("砂轮角度当前位置");
        _profilerCurrentPosition = AddReadOnly("廓形仪当前位置");

        // 只写参数
        AddWriteOnly("小车点动速度");
        AddWriteOnly("小车定位运行地址");
        AddWriteOnly("小车定位运行速度");
        AddWriteOnly("小车定位运行启动");
        AddWriteOnly("小车点动前进");
        AddWriteOnly("小车点动后退");
        AddWriteOnly("小车定位回零");
        AddWriteOnly("小车故障复位");

        AddWriteOnly("砂轮纵向点动速度");
        AddWriteOnly("砂轮纵向定位运行地址");
        AddWriteOnly("砂轮纵向定位运行速度");
        AddWriteOnly("砂轮纵向定位运行启动");
        AddWriteOnly("砂轮纵向点动下行");
        AddWriteOnly("砂轮纵向点动上行");
        AddWriteOnly("砂轮纵向定位回零");
        AddWriteOnly("砂轮纵向故障复位");

        AddWriteOnly("砂轮横向点动速度");
        AddWriteOnly("砂轮横向定位运行地址");
        AddWriteOnly("砂轮横向定位运行速度");
        AddWriteOnly("砂轮横向定位运行启动");
        AddWriteOnly("砂轮横向点动左行");
        AddWriteOnly("砂轮横向点动右行");
        AddWriteOnly("砂轮横向定位回零");
        AddWriteOnly("砂轮横向故障复位");

        AddWriteOnly("砂轮角度点动速度");
        AddWriteOnly("砂轮角度定位运行地址");
        AddWriteOnly("砂轮角度定位运行速度");
        AddWriteOnly("砂轮角度定位运行启动");
        AddWriteOnly("砂轮角度点动逆行");
        AddWriteOnly("砂轮角度点动顺行");
        AddWriteOnly("砂轮角度定位回零");
        AddWriteOnly("砂轮角度故障复位");

        AddWriteOnly("廓形仪点动速度");
        AddWriteOnly("廓形仪定位运行地址");
        AddWriteOnly("廓形仪定位运行速度");
        AddWriteOnly("廓形仪定位运行启动");
        AddWriteOnly("廓形仪点动下行");
        AddWriteOnly("廓形仪点动上行");
        AddWriteOnly("廓形仪定位回零");
        AddWriteOnly("廓形仪故障复位");
        
        AddWriteOnly("砂轮旋转速度");
        AddWriteOnly("砂轮质量控制");
    }

    public ObservableCollection<MotorParameterItem> Parameters => _items;
    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (_connectionStatus == value) return;
            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    public async Task StartPollingAsync(string ipAddress, int port, byte unitId, int pollIntervalMs)
    {
        if (_pollingTask != null && !_pollingTask.IsCompleted) return;

        _plc = new PlcModbusCommunicator(ipAddress, port, unitId);
        _uiContext.Post(_ => ConnectionStatus = $"连接中 {ipAddress}:{port} (Unit {unitId})", null);
        try
        {
            await _plc.ConnectAsync();
        }
        catch (Exception ex)
        {
            _uiContext.Post(_ => ConnectionStatus = $"连接失败: {ex.Message}", null);
            return;
        }

        _uiContext.Post(_ => ConnectionStatus = $"已连接 {ipAddress}:{port} (Unit {unitId})", null);
        _pollingCts = new CancellationTokenSource();
        _pollingTask = PollReadableParametersAsync(_plc, _parameters, pollIntervalMs, _pollingCts.Token);
    }

    public void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
        try { _plc?.Disconnect(); } catch { }
        _uiContext.Post(_ => ConnectionStatus = "未连接", null);
    }

    public async Task PollReadableParametersAsync(
        PlcModbusCommunicator plc,
        Motor parameters,
        int pollIntervalMs,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                parameters.CarCurrentPosition = plc.ReadInt32(100);   // D100
                parameters.WheelLongitudinalCurrentPosition = plc.ReadInt32(300); // D300
                parameters.WheelLateralCurrentPosition = plc.ReadInt32(400);      // D400
                parameters.WheelAngleCurrentPosition = plc.ReadInt32(500);        // D500
                parameters.ProfilerCurrentPosition = plc.ReadInt32(600);          // D600

                _uiContext.Post(_ =>
                {
                    _carCurrentPosition.Value = parameters.CarCurrentPosition.ToString();
                    _wheelLongitudinalCurrentPosition.Value = parameters.WheelLongitudinalCurrentPosition.ToString();
                    _wheelLateralCurrentPosition.Value = parameters.WheelLateralCurrentPosition.ToString();
                    _wheelAngleCurrentPosition.Value = parameters.WheelAngleCurrentPosition.ToString();
                    _profilerCurrentPosition.Value = parameters.ProfilerCurrentPosition.ToString();
                }, null);
            }
            catch
            {
                // 读取异常时保持当前值
            }

            await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private MotorParameterItem AddReadOnly(string name)
    {
        var item = new MotorParameterItem(name, true);
        _items.Add(item);
        return item;
    }

    private void AddWriteOnly(string name)
    {
        _items.Add(new MotorParameterItem(name, false));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class MotorParameterItem : INotifyPropertyChanged
{
    private string _value;
    private string _inputValue;

    public MotorParameterItem(string name, bool isReadOnly)
    {
        Name = name;
        IsReadOnly = isReadOnly;
    }

    public string Name { get; }
    public bool IsReadOnly { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public string InputValue
    {
        get => _inputValue;
        set
        {
            if (_inputValue == value) return;
            _inputValue = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
