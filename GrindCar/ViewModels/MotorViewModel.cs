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
using GrindCar.Definitions;
using GrindCar.Infrastructure;
using GrindCar.Models;
using GrindCar.Services;

namespace GrindCar.ViewModels;

/// <summary>
/// 电机调试参数的 ViewModel：负责连接 PLC、轮询读取与写入参数
/// </summary>
public class MotorViewModel : INotifyPropertyChanged
{
    private const string DefaultIpAddress = "127.0.0.1";
    private const string DefaultPortText = "502";
    private const int MinPort = 1;
    private const int MaxPort = 65535;
    private const byte DefaultUnitId = 1;
    private const int DefaultPollIntervalMs = 1000;

    // 参数模型（当前读值缓存）
    private readonly Motor _parameters = new();
    // UI 列表数据源
    private readonly ObservableCollection<MotorParameterItemViewModel> _items = new();
    // 只读参数条目引用（便于快速更新）
    private readonly MotorParameterItemViewModel _carCurrentPosition;
    private readonly MotorParameterItemViewModel _wheelLongitudinalCurrentPosition;
    private readonly MotorParameterItemViewModel _wheelLateralCurrentPosition;
    private readonly MotorParameterItemViewModel _wheelAngleCurrentPosition;
    private readonly MotorParameterItemViewModel _profilerCurrentPosition;

    // 轮询任务与通信对象
    private CancellationTokenSource _pollingCts;
    private Task _pollingTask;
    private IPlcClient _plc;
    // UI 线程上下文
    private readonly SynchronizationContext _uiContext;
    // 连接状态展示
    private string _connectionStatus = "未连接";
    private string _ipAddress = DefaultIpAddress;
    private string _port = DefaultPortText;
    private bool _isConnected;
    private bool _isConnecting;
    // 写入参数映射（名称 -> 地址/类型/比例）
    private readonly Dictionary<string, ParameterWriteSpec> _writeSpecs = new();
    // 读取参数比例（名称 -> 除数）
    private readonly Dictionary<string, double> _readScales = new();

    // 写入数据类型
    private enum DataKind
    {
        Int32,
        Int16,
        Bool
    }

    // 写入参数规格
    private readonly struct ParameterWriteSpec
    {
        public ParameterWriteSpec(ushort address, DataKind kind, double scale)
        {
            Address = address;
            Kind = kind;
            Scale = scale;
        }

        public ushort Address { get; }
        public DataKind Kind { get; }
        public double Scale { get; }
    }

    public MotorViewModel()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        // 只读参数置前
        _carCurrentPosition = AddReadOnly(MotorParameterDefinitions.CarCurrentPositionName);
        _wheelLongitudinalCurrentPosition = AddReadOnly(MotorParameterDefinitions.WheelLongitudinalCurrentPositionName);
        _wheelLateralCurrentPosition = AddReadOnly(MotorParameterDefinitions.WheelLateralCurrentPositionName);
        _wheelAngleCurrentPosition = AddReadOnly(MotorParameterDefinitions.WheelAngleCurrentPositionName);
        _profilerCurrentPosition = AddReadOnly(MotorParameterDefinitions.ProfilerCurrentPositionName);

        // 只写参数
        foreach (string name in MotorParameterDefinitions.WritableParameterNames)
        {
            AddWriteOnly(name);
        }

        // 构建地址映射
        BuildWriteSpecs();
        // 构建读取比例
        BuildReadScales();
        // 绑定“修改”命令
        ModifyCommand = new RelayCommand(param =>
        {
            if (param is MotorParameterItemViewModel item)
            {
                _ = ExecuteModifyAsync(item);
            }
        }, param => param is MotorParameterItemViewModel item && !item.IsReadOnly);

        ConnectCommand = new RelayCommand(_ => _ = ConnectAsync(), _ => !IsConnected && !IsConnecting);
        DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected || IsConnecting);
    }

    /// <summary>
    /// 参数列表（绑定到界面）
    /// </summary>
    public ObservableCollection<MotorParameterItemViewModel> Parameters => _items;

    /// <summary>
    /// 点击“修改”时触发的命令
    /// </summary>
    public ICommand ModifyCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }

    public event Action<string> ConnectionFailed;

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (_ipAddress == value) return;
            _ipAddress = value;
            OnPropertyChanged();
        }
    }

    public string Port
    {
        get => _port;
        set
        {
            if (_port == value) return;
            _port = value;
            OnPropertyChanged();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected == value) return;
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
            if (_isConnecting == value) return;
            _isConnecting = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsBusy => IsConnected || IsConnecting;

    /// <summary>
    /// 连接状态文本
    /// </summary>
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

    private void PostStatus(string message)
    {
        _uiContext.Post(_ => ConnectionStatus = message, null);
    }

    private void HandleException(string userMessage, Exception ex)
    {
        if (ex != null)
        {
            Debug.WriteLine($"{userMessage} {ex}");
        }
        PostStatus(userMessage);
    }

    private async Task ConnectAsync()
    {
        if (IsConnected || IsConnecting) return;

        string ip = IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            ConnectionStatus = "IP 不能为空";
            ConnectionFailed?.Invoke("IP 不能为空。");
            return;
        }

        if (!int.TryParse(Port?.Trim(), out int port) || port < MinPort || port > MaxPort)
        {
            ConnectionStatus = "端口无效";
            ConnectionFailed?.Invoke($"端口无效，请输入 {MinPort}-{MaxPort} 的整数。");
            return;
        }

        IsConnecting = true;
        bool ok;
        try
        {
            ok = await StartPollingAsync(ip, port, DefaultUnitId, DefaultPollIntervalMs);
        }
        finally
        {
            IsConnecting = false;
        }

        if (!ok)
        {
            ConnectionFailed?.Invoke(ConnectionStatus);
        }
    }

    private void Disconnect()
    {
        StopPolling();
    }

    /// <summary>
    /// 启动 PLC 连接并开始轮询读取
    /// </summary>
    public async Task<bool> StartPollingAsync(string ipAddress, int port, byte unitId, int pollIntervalMs)
    {
        if (_pollingTask != null && !_pollingTask.IsCompleted) return true;

        _plc = new PlcModbusCommunicator(ipAddress, port, unitId);
        PostStatus($"连接中 {ipAddress}:{port} (Unit {unitId})");
        try
        {
            await _plc.ConnectAsync();
        }
        catch (Exception ex)
        {
            HandleException($"连接失败: {ex.Message}", ex);
            IsConnected = false;
            return false;
        }

        PostStatus($"已连接 {ipAddress}:{port} (Unit {unitId})");
        _pollingCts = new CancellationTokenSource();
        _pollingTask = PollReadableParametersAsync(_plc, _parameters, pollIntervalMs, _pollingCts.Token);
        IsConnected = true;
        return true;
    }

    /// <summary>
    /// 停止轮询并断开连接
    /// </summary>
    public void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
        try { _plc?.Disconnect(); } catch { }
        PostStatus("未连接");
        IsConnected = false;
        IsConnecting = false;
    }

    /// <summary>
    /// 轮询读取只读参数，并更新到界面3
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
                // 读取 PLC 原始值
                parameters.CarCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.CarCurrentPositionAddress);
                parameters.WheelLongitudinalCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.WheelLongitudinalCurrentPositionAddress);
                parameters.WheelLateralCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.WheelLateralCurrentPositionAddress);
                parameters.WheelAngleCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.WheelAngleCurrentPositionAddress);
                parameters.ProfilerCurrentPosition = plc.ReadInt32(MotorParameterDefinitions.ProfilerCurrentPositionAddress);

                _uiContext.Post(_ =>
                {
                    // 按比例换算后显示
                    _carCurrentPosition.Value = FormatScaled(parameters.CarCurrentPosition, MotorParameterDefinitions.CarCurrentPositionName);
                    _wheelLongitudinalCurrentPosition.Value = FormatScaled(parameters.WheelLongitudinalCurrentPosition, MotorParameterDefinitions.WheelLongitudinalCurrentPositionName);
                    _wheelLateralCurrentPosition.Value = FormatScaled(parameters.WheelLateralCurrentPosition, MotorParameterDefinitions.WheelLateralCurrentPositionName);
                    _wheelAngleCurrentPosition.Value = FormatScaled(parameters.WheelAngleCurrentPosition, MotorParameterDefinitions.WheelAngleCurrentPositionName);
                    _profilerCurrentPosition.Value = FormatScaled(parameters.ProfilerCurrentPosition, MotorParameterDefinitions.ProfilerCurrentPositionName);
                }, null);
            }
            catch
            {
                // 读取异常时保持当前值
            }

            await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private MotorParameterItemViewModel AddReadOnly(string name)
    {
        var item = new MotorParameterItemViewModel(name, true, GetUnit(name));
        _items.Add(item);
        return item;
    }

    private void AddWriteOnly(string name)
    {
        _items.Add(new MotorParameterItemViewModel(name, false, GetUnit(name)));
    }

    private static string GetUnit(string name)
    {
        return MotorParameterDefinitions.ParameterUnits.TryGetValue(name, out string unit)
            ? unit
            : string.Empty;
    }

    /// <summary>
    /// 读取参数显示比例（原始值 / scale）
    /// </summary>
    private void BuildReadScales()
    {
        _readScales[MotorParameterDefinitions.CarCurrentPositionName] = MotorParameterDefinitions.CarCurrentPositionScale;
        _readScales[MotorParameterDefinitions.WheelLongitudinalCurrentPositionName] = MotorParameterDefinitions.WheelLongitudinalCurrentPositionScale;
        _readScales[MotorParameterDefinitions.WheelLateralCurrentPositionName] = MotorParameterDefinitions.WheelLateralCurrentPositionScale;
        _readScales[MotorParameterDefinitions.WheelAngleCurrentPositionName] = MotorParameterDefinitions.WheelAngleCurrentPositionScale;
        _readScales[MotorParameterDefinitions.ProfilerCurrentPositionName] = MotorParameterDefinitions.ProfilerCurrentPositionScale;
    }

    /// <summary>
    /// 将原始值按比例换算为显示值
    /// </summary>
    private string FormatScaled(int rawValue, string name)
    {
        if (_readScales.TryGetValue(name, out double scale) && scale != 0)
        {
            double scaled = rawValue / scale;
            return scaled.ToString("0.###", CultureInfo.CurrentCulture);
        }
        return rawValue.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 写入参数映射：名称 -> 地址/类型/比例
    /// </summary>
    private void BuildWriteSpecs()
    {
        // 小车
        _writeSpecs[MotorParameterDefinitions.CarJogSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarJogSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.CarJogSpeedScale);
        _writeSpecs[MotorParameterDefinitions.CarPositionTargetAddressName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarPositionTargetAddress,
            DataKind.Int32,
            MotorParameterDefinitions.CarPositionTargetAddressScale);
        _writeSpecs[MotorParameterDefinitions.CarPositionSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarPositionSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.CarPositionSpeedScale);
        _writeSpecs[MotorParameterDefinitions.CarPositionStartName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarPositionStartAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.CarJogForwardName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarJogForwardAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.CarJogBackwardName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarJogBackwardAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.CarHomeName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarHomeAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.CarFaultResetName] = new ParameterWriteSpec(
            MotorParameterDefinitions.CarFaultResetAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);

        // 砂轮纵向
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalJogSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalJogSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelLongitudinalJogSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalPositionTargetAddressName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalPositionTargetAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelLongitudinalPositionTargetAddressScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalPositionSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalPositionSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelLongitudinalPositionSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalPositionStartName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalPositionStartAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalJogDownName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalJogDownAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalJogUpName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalJogUpAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalHomeName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalHomeAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLongitudinalFaultResetName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLongitudinalFaultResetAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);

        // 砂轮横向
        _writeSpecs[MotorParameterDefinitions.WheelLateralJogSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralJogSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelLateralJogSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralPositionTargetAddressName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralPositionTargetAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelLateralPositionTargetAddressScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralPositionSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralPositionSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelLateralPositionSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralPositionStartName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralPositionStartAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralJogLeftName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralJogLeftAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralJogRightName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralJogRightAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralHomeName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralHomeAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelLateralFaultResetName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelLateralFaultResetAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);

        // 砂轮角度
        _writeSpecs[MotorParameterDefinitions.WheelAngleJogSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAngleJogSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelAngleJogSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelAnglePositionTargetAddressName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAnglePositionTargetAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelAnglePositionTargetAddressScale);
        _writeSpecs[MotorParameterDefinitions.WheelAnglePositionSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAnglePositionSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.WheelAnglePositionSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelAnglePositionStartName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAnglePositionStartAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelAngleJogReverseName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAngleJogReverseAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelAngleJogForwardName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAngleJogForwardAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelAngleHomeName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAngleHomeAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.WheelAngleFaultResetName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelAngleFaultResetAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);

        // 廓形仪
        _writeSpecs[MotorParameterDefinitions.ProfilerJogSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerJogSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.ProfilerJogSpeedScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerPositionTargetAddressName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerPositionTargetAddress,
            DataKind.Int32,
            MotorParameterDefinitions.ProfilerPositionTargetAddressScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerPositionSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerPositionSpeedAddress,
            DataKind.Int32,
            MotorParameterDefinitions.ProfilerPositionSpeedScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerPositionStartName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerPositionStartAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerJogDownName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerJogDownAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerJogUpName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerJogUpAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerHomeName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerHomeAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);
        _writeSpecs[MotorParameterDefinitions.ProfilerFaultResetName] = new ParameterWriteSpec(
            MotorParameterDefinitions.ProfilerFaultResetAddress,
            DataKind.Bool, MotorParameterDefinitions.BoolScale);

        // 砂轮旋转速度 / 质量控制
        _writeSpecs[MotorParameterDefinitions.WheelRotationSpeedName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelRotationSpeedAddress,
            DataKind.Int16,
            MotorParameterDefinitions.WheelRotationSpeedScale);
        _writeSpecs[MotorParameterDefinitions.WheelQualityControlName] = new ParameterWriteSpec(
            MotorParameterDefinitions.WheelQualityControlAddress,
            DataKind.Int16,
            MotorParameterDefinitions.WheelQualityControlScale);
    }

    /// <summary>
    /// 执行“修改”：解析输入、换算、写入 PLC
    /// </summary>
    private async Task ExecuteModifyAsync(MotorParameterItemViewModel item)
    {
        if (!_writeSpecs.TryGetValue(item.Name, out var spec))
        {
            PostStatus($"未找到参数映射: {item.Name}");
            return;
        }

        if (_plc == null || !_plc.IsConnected)
        {
            PostStatus("未连接，无法写入");
            return;
        }

        string input = item.InputValue?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            PostStatus("请输入有效数值/开关");
            return;
        }

        try
        {
            switch (spec.Kind)
            {
                case DataKind.Bool:
                {
                    // 布尔量直接写入 Coil
                    if (!TryParseBool(input, out bool boolValue))
                    {
                        PostStatus("布尔输入无效，请输入 true/false 或 1/0");
                        return;
                    }
                    await _plc.WriteSingleCoilAsync(spec.Address, boolValue).ConfigureAwait(false);
                    break;
                }
                case DataKind.Int32:
                {
                    // 数值按比例放大为原始值后写入
                    if (!TryParseNumber(input, out double number))
                    {
                        PostStatus("数值输入无效");
                        return;
                    }
                    double raw = number * spec.Scale;
                    if (raw > int.MaxValue || raw < int.MinValue)
                    {
                        PostStatus("数值超出 Int32 范围");
                        return;
                    }
                    int rawInt = (int)Math.Round(raw);
                    _plc.WriteInt32(spec.Address, rawInt);
                    break;
                }
                case DataKind.Int16:
                {
                    // 16 位整型写入（如砂轮旋转速度/质量控制）
                    if (!TryParseNumber(input, out double number))
                    {
                        PostStatus("数值输入无效");
                        return;
                    }
                    double raw = number * spec.Scale;
                    if (raw > short.MaxValue || raw < short.MinValue)
                    {
                        PostStatus("数值超出 Int16 范围");
                        return;
                    }
                    short rawShort = (short)Math.Round(raw);
                    _plc.WriteInt16(spec.Address, rawShort);
                    break;
                }
                default:
                    PostStatus("不支持的数据类型");
                    return;
            }

            _uiContext.Post(_ =>
            {
                ConnectionStatus = $"写入成功: {item.Name}";
                item.InputValue = string.Empty;
            }, null);
        }
        catch (Exception ex)
        {
            HandleException($"写入失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析数值（兼容本地文化与 InvariantCulture）
    /// </summary>
    private static bool TryParseNumber(string input, out double value)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// 解析布尔值（支持 1/0、true/false、是/否 等）
    /// </summary>
    private static bool TryParseBool(string input, out bool value)
    {
        string normalized = input.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "1":
            case "true":
            case "on":
            case "yes":
            case "y":
            case "是":
                value = true;
                return true;
            case "0":
            case "false":
            case "off":
            case "no":
            case "n":
            case "否":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    /// <summary>
    /// 属性变化通知
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// 触发属性变化通知
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


