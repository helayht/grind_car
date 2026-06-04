using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using GrindCar.Definitions;
using GrindCar.Infrastructure;
using GrindCar.Models;
using GrindCar.Services;
using GrindCar.Services.Motor;

namespace GrindCar.ViewModels;

/// <summary>
/// 电机调试参数的 ViewModel：负责连接 PLC、轮询读取与写入参数。
/// </summary>
public class MotorViewModel : INotifyPropertyChanged
{
    private const string DefaultIpAddress = "127.0.0.1";
    private const string DefaultPortText = "502";
    private const byte DefaultUnitId = 1;
    private const int DefaultPollIntervalMs = 1000;
    private const int DashboardTickIntervalMs = 1200;

    private readonly Motor _parameters = new();
    private readonly ObservableCollection<MotorParameterItemViewModel> _items = new();
    private readonly MotorParameterItemViewModel _carCurrentPosition;
    private readonly MotorParameterItemViewModel _wheelLongitudinalCurrentPosition;
    private readonly MotorParameterItemViewModel _wheelLateralCurrentPosition;
    private readonly MotorParameterItemViewModel _wheelAngleCurrentPosition;
    private readonly MotorParameterItemViewModel _profilerCurrentPosition;

    private readonly SynchronizationContext _uiContext;
    private readonly DispatcherTimer _dashboardTimer;
    private readonly IReadOnlyDictionary<string, MotorParameterWriteSpec> _writeSpecs;
    private readonly IReadOnlyDictionary<string, double> _readScales;

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private IPlcClient? _plc;

    private string _connectionStatus = "未连接";
    private string _ipAddress = DefaultIpAddress;
    private string _port = DefaultPortText;
    private bool _isConnected;
    private bool _isConnecting;
    private string _speedUnit = string.Empty;
    private string _speedStatus = "暂无数据";
    private string _dashboardTimestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>
    /// 初始化电机调试视图模型，构建参数集合、命令和首页演示数据。
    /// </summary>
    public MotorViewModel()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _writeSpecs = MotorParameterSpecProvider.BuildWriteSpecs();
        _readScales = MotorParameterSpecProvider.BuildReadScales();

        _carCurrentPosition = AddReadOnly(MotorParameterDefinitions.CarCurrentPositionName);
        _wheelLongitudinalCurrentPosition = AddReadOnly(MotorParameterDefinitions.WheelLongitudinalCurrentPositionName);
        _wheelLateralCurrentPosition = AddReadOnly(MotorParameterDefinitions.WheelLateralCurrentPositionName);
        _wheelAngleCurrentPosition = AddReadOnly(MotorParameterDefinitions.WheelAngleCurrentPositionName);
        _profilerCurrentPosition = AddReadOnly(MotorParameterDefinitions.ProfilerCurrentPositionName);

        for (int index = 0; index < MotorParameterDefinitions.WritableParameterNames.Count; index++)
        {
            AddWriteOnly(MotorParameterDefinitions.WritableParameterNames[index]);
        }

        ModifyCommand = new RelayCommand(
            execute: parameter =>
            {
                if (parameter is MotorParameterItemViewModel item)
                {
                    _ = ExecuteModifyAsync(item);
                }
            },
            canExecute: parameter => parameter is MotorParameterItemViewModel item && !item.IsReadOnly);

        ConnectCommand = new RelayCommand(_ => _ = ConnectAsync(), _ => !IsConnected && !IsConnecting);
        DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected || IsConnecting);

        _dashboardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DashboardTickIntervalMs)
        };
        _dashboardTimer.Tick += DashboardTimerTick;
        _dashboardTimer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<string>? ConnectionFailed;

    public ObservableCollection<MotorParameterItemViewModel> Parameters => _items;

    public ICommand ModifyCommand { get; }

    public ICommand ConnectCommand { get; }

    public ICommand DisconnectCommand { get; }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (_ipAddress == value)
            {
                return;
            }

            _ipAddress = value;
            OnPropertyChanged();
        }
    }

    public string Port
    {
        get => _port;
        set
        {
            if (_port == value)
            {
                return;
            }

            _port = value;
            OnPropertyChanged();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected == value)
            {
                return;
            }

            _isConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        private set
        {
            if (_isConnecting == value)
            {
                return;
            }

            _isConnecting = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsBusy => IsConnected || IsConnecting;

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

    public double BatteryLevel => 0.0;

    public double BatteryVoltage => 0.0;

    public double CurrentSpeed => 0.0;

    public string SpeedUnit
    {
        get => _speedUnit;
        private set
        {
            if (_speedUnit == value)
            {
                return;
            }

            _speedUnit = value;
            OnPropertyChanged();
        }
    }

    public string SpeedStatus
    {
        get => _speedStatus;
        private set
        {
            if (_speedStatus == value)
            {
                return;
            }

            _speedStatus = value;
            OnPropertyChanged();
        }
    }

    public string DashboardTimestamp
    {
        get => _dashboardTimestamp;
        private set
        {
            if (_dashboardTimestamp == value)
            {
                return;
            }

            _dashboardTimestamp = value;
            OnPropertyChanged();
        }
    }

    public string BatteryLevelText => "--";

    public string BatteryVoltageText => "--";

    public double BatteryFillWidth => 0.0;

    public string BatteryStateText => "暂无数据";

    public string BatteryAccentColor => "#8EA7C2";

    public string CurrentSpeedText => "--";

    public double SpeedGaugeAngle => -90.0;

    public string SpeedAccentColor => "#8EA7C2";

    /// <summary>
    /// 启动 PLC 连接并开始轮询读取。
    /// </summary>
    public async Task<bool> StartPollingAsync(string ipAddress, int port, byte unitId, int pollIntervalMs)
    {
        if (_pollingTask != null && !_pollingTask.IsCompleted)
        {
            return true;
        }

        _plc = new PlcModbusCommunicator(ipAddress, port, unitId);
        PostStatus($"连接中 {ipAddress}:{port} (Unit {unitId})");

        try
        {
            await _plc.ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HandleException($"连接失败: {ex.Message}", ex);
            IsConnected = false;
            return false;
        }

        _pollingCts = new CancellationTokenSource();
        _pollingTask = PollReadableParametersAsync(_plc, _parameters, pollIntervalMs, _pollingCts.Token);
        IsConnected = true;
        PostStatus($"已连接 {ipAddress}:{port} (Unit {unitId})");
        return true;
    }

    /// <summary>
    /// 停止轮询并断开连接。
    /// </summary>
    public void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;

        try
        {
            _plc?.Disconnect();
        }
        catch
        {
            // 忽略断连异常，避免影响 UI 状态恢复。
        }

        PostStatus("未连接");
        IsConnected = false;
        IsConnecting = false;
    }

    /// <summary>
    /// 在视图模型销毁前停止首页定时器并释放 PLC 资源。
    /// </summary>
    public void Shutdown()
    {
        _dashboardTimer.Stop();
        StopPolling();
    }

    /// <summary>
    /// 轮询读取只读参数并更新界面。
    /// </summary>
    public async Task PollReadableParametersAsync(
        IPlcClient plc,
        Motor parameters,
        int pollIntervalMs,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                parameters.CarCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.CarCurrentPositionAddress);
                parameters.WheelLongitudinalCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.WheelLongitudinalCurrentPositionAddress);
                parameters.WheelLateralCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.WheelLateralCurrentPositionAddress);
                parameters.WheelAngleCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.WheelAngleCurrentPositionAddress);
                parameters.ProfilerCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.ProfilerCurrentPositionAddress);

                _uiContext.Post(_ =>
                {
                    _carCurrentPosition.Value = FormatScaled(parameters.CarCurrentPosition, MotorParameterDefinitions.CarCurrentPositionName);
                    _wheelLongitudinalCurrentPosition.Value = FormatScaled(parameters.WheelLongitudinalCurrentPosition, MotorParameterDefinitions.WheelLongitudinalCurrentPositionName);
                    _wheelLateralCurrentPosition.Value = FormatScaled(parameters.WheelLateralCurrentPosition, MotorParameterDefinitions.WheelLateralCurrentPositionName);
                    _wheelAngleCurrentPosition.Value = FormatScaled(parameters.WheelAngleCurrentPosition, MotorParameterDefinitions.WheelAngleCurrentPositionName);
                    _profilerCurrentPosition.Value = FormatScaled(parameters.ProfilerCurrentPosition, MotorParameterDefinitions.ProfilerCurrentPositionName);
                }, null);
            }
            catch
            {
                // 单次读取失败时保持当前值。
            }

            try
            {
                await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAsync()
    {
        if (IsConnected || IsConnecting)
        {
            return;
        }

        PlcConnectionValidationResult validationResult = PlcConnectionSettingsValidator.Validate(IpAddress, Port);
        if (!validationResult.IsValid)
        {
            ConnectionStatus = validationResult.ErrorMessage;
            ConnectionFailed?.Invoke(validationResult.ErrorMessage);
            return;
        }

        IsConnecting = true;
        bool connected;
        try
        {
            connected = await StartPollingAsync(
                validationResult.IpAddress,
                validationResult.Port,
                DefaultUnitId,
                DefaultPollIntervalMs).ConfigureAwait(false);
        }
        finally
        {
            IsConnecting = false;
        }

        if (!connected)
        {
            ConnectionFailed?.Invoke(ConnectionStatus);
        }
    }

    private void Disconnect()
    {
        StopPolling();
    }

    private async Task ExecuteModifyAsync(MotorParameterItemViewModel item)
    {
        if (!_writeSpecs.TryGetValue(item.Name, out MotorParameterWriteSpec spec))
        {
            PostStatus($"未找到参数映射: {item.Name}");
            return;
        }

        if (_plc == null || !_plc.IsConnected)
        {
            PostStatus("未连接，无法写入");
            return;
        }

        try
        {
            switch (spec.Kind)
            {
                case MotorParameterDataKind.Bool:
                    bool targetValue = !item.BoolValue;
                    await _plc.WriteSingleCoilAsync(spec.Address, targetValue).ConfigureAwait(false);
                    _uiContext.Post(_ =>
                    {
                        item.BoolValue = targetValue;
                        ConnectionStatus = $"写入成功: {item.Name}";
                    }, null);
                    break;

                case MotorParameterDataKind.Int32:
                    string int32Input = item.InputValue.Trim();
                    if (string.IsNullOrWhiteSpace(int32Input))
                    {
                        PostStatus("请输入有效数值");
                        return;
                    }

                    if (!MotorInputParser.TryParseNumber(int32Input, out double int32Value))
                    {
                        PostStatus("数值输入无效");
                        return;
                    }

                    double int32Raw = int32Value * spec.Scale;
                    if (int32Raw > int.MaxValue || int32Raw < int.MinValue)
                    {
                        PostStatus("数值超出 Int32 范围");
                        return;
                    }

                    _plc.WriteInt32(spec.Address, (int)Math.Round(int32Raw));
                    _uiContext.Post(_ =>
                    {
                        ConnectionStatus = $"写入成功: {item.Name}";
                        item.InputValue = string.Empty;
                    }, null);
                    break;

                case MotorParameterDataKind.Int16:
                    string int16Input = item.InputValue.Trim();
                    if (string.IsNullOrWhiteSpace(int16Input))
                    {
                        PostStatus("请输入有效数值");
                        return;
                    }

                    if (!MotorInputParser.TryParseNumber(int16Input, out double int16Value))
                    {
                        PostStatus("数值输入无效");
                        return;
                    }

                    double int16Raw = int16Value * spec.Scale;
                    if (int16Raw > short.MaxValue || int16Raw < short.MinValue)
                    {
                        PostStatus("数值超出 Int16 范围");
                        return;
                    }

                    _plc.WriteInt16(spec.Address, (short)Math.Round(int16Raw));
                    _uiContext.Post(_ =>
                    {
                        ConnectionStatus = $"写入成功: {item.Name}";
                        item.InputValue = string.Empty;
                    }, null);
                    break;

                default:
                    PostStatus("不支持的数据类型");
                    return;
            }
        }
        catch (Exception ex)
        {
            HandleException($"写入失败: {ex.Message}", ex);
        }
    }

    private void DashboardTimerTick(object? sender, EventArgs e)
    {
        DashboardTimestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private MotorParameterItemViewModel AddReadOnly(string name)
    {
        var item = new MotorParameterItemViewModel(name, isReadOnly: true, GetUnit(name));
        _items.Add(item);
        return item;
    }

    private void AddWriteOnly(string name)
    {
        bool isBoolWrite = _writeSpecs.TryGetValue(name, out MotorParameterWriteSpec spec)
            && spec.Kind == MotorParameterDataKind.Bool;
        _items.Add(new MotorParameterItemViewModel(name, isReadOnly: false, GetUnit(name), isBoolWrite));
    }

    private static string GetUnit(string name)
    {
        return MotorParameterDefinitions.ParameterUnits.TryGetValue(name, out string? unit)
            ? unit ?? string.Empty
            : string.Empty;
    }

    private string FormatScaled(int rawValue, string name)
    {
        if (_readScales.TryGetValue(name, out double scale) && Math.Abs(scale) > double.Epsilon)
        {
            double scaled = rawValue / scale;
            return scaled.ToString("0.###", CultureInfo.CurrentCulture);
        }

        return rawValue.ToString(CultureInfo.CurrentCulture);
    }

    private void PostStatus(string message)
    {
        _uiContext.Post(_ => ConnectionStatus = message, null);
    }

    private void HandleException(string userMessage, Exception ex)
    {
        Debug.WriteLine($"{userMessage} {ex}");
        PostStatus(userMessage);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
