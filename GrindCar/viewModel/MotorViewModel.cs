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
using GrindCar.Model;
using GrindCar.tool;

namespace GrindCar.viewModel;

/// <summary>
/// 电机调试参数的 ViewModel：负责连接 PLC、轮询读取与写入参数
/// </summary>
public class MotorViewModel : INotifyPropertyChanged
{
    // 参数模型（当前读值缓存）
    private readonly Motor _parameters = new();
    // UI 列表数据源
    private readonly ObservableCollection<MotorParameterItem> _items = new();
    // 只读参数条目引用（便于快速更新）
    private readonly MotorParameterItem _carCurrentPosition;
    private readonly MotorParameterItem _wheelLongitudinalCurrentPosition;
    private readonly MotorParameterItem _wheelLateralCurrentPosition;
    private readonly MotorParameterItem _wheelAngleCurrentPosition;
    private readonly MotorParameterItem _profilerCurrentPosition;

    // 轮询任务与通信对象
    private CancellationTokenSource _pollingCts;
    private Task _pollingTask;
    private PlcModbusCommunicator _plc;
    // UI 线程上下文
    private readonly SynchronizationContext _uiContext;
    // 连接状态展示
    private string _connectionStatus = "未连接";
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

        // 构建地址映射
        BuildWriteSpecs();
        // 构建读取比例
        BuildReadScales();
        // 绑定“修改”命令
        ModifyCommand = new RelayCommand(param =>
        {
            if (param is MotorParameterItem item)
            {
                _ = ExecuteModifyAsync(item);
            }
        }, param => param is MotorParameterItem item && !item.IsReadOnly);
    }

    /// <summary>
    /// 参数列表（绑定到界面）
    /// </summary>
    public ObservableCollection<MotorParameterItem> Parameters => _items;

    /// <summary>
    /// 点击“修改”时触发的命令
    /// </summary>
    public ICommand ModifyCommand { get; }

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

    /// <summary>
    /// 启动 PLC 连接并开始轮询读取
    /// </summary>
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

    /// <summary>
    /// 停止轮询并断开连接
    /// </summary>
    public void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
        try { _plc?.Disconnect(); } catch { }
        _uiContext.Post(_ => ConnectionStatus = "未连接", null);
    }

    /// <summary>
    /// 轮询读取只读参数，并更新到界面3
    /// </summary>
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
                // 读取 PLC 原始值
                parameters.CarCurrentPosition = plc.ReadInt32(100);   // D100
                parameters.WheelLongitudinalCurrentPosition = plc.ReadInt32(300); // D300
                parameters.WheelLateralCurrentPosition = plc.ReadInt32(400);      // D400
                parameters.WheelAngleCurrentPosition = plc.ReadInt32(500);        // D500
                parameters.ProfilerCurrentPosition = plc.ReadInt32(600);          // D600

                _uiContext.Post(_ =>
                {
                    // 按比例换算后显示
                    _carCurrentPosition.Value = FormatScaled(parameters.CarCurrentPosition, "小车当前位置");
                    _wheelLongitudinalCurrentPosition.Value = FormatScaled(parameters.WheelLongitudinalCurrentPosition, "砂轮纵向当前位置");
                    _wheelLateralCurrentPosition.Value = FormatScaled(parameters.WheelLateralCurrentPosition, "砂轮横向当前位置");
                    _wheelAngleCurrentPosition.Value = FormatScaled(parameters.WheelAngleCurrentPosition, "砂轮角度当前位置");
                    _profilerCurrentPosition.Value = FormatScaled(parameters.ProfilerCurrentPosition, "廓形仪当前位置");
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

    /// <summary>
    /// 读取参数显示比例（原始值 / scale）
    /// </summary>
    private void BuildReadScales()
    {
        _readScales["小车当前位置"] = 100000.0;
        _readScales["砂轮纵向当前位置"] = 10000.0;
        _readScales["砂轮横向当前位置"] = 10000.0;
        _readScales["砂轮角度当前位置"] = 10000.0;
        _readScales["廓形仪当前位置"] = 10000.0;
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
        _writeSpecs["小车点动速度"] = new ParameterWriteSpec(1000, DataKind.Int32, 1000);
        _writeSpecs["小车定位运行地址"] = new ParameterWriteSpec(1002, DataKind.Int32, 100000);
        _writeSpecs["小车定位运行速度"] = new ParameterWriteSpec(1004, DataKind.Int32, 1000);
        _writeSpecs["小车定位运行启动"] = new ParameterWriteSpec(104, DataKind.Bool, 1);
        _writeSpecs["小车点动前进"] = new ParameterWriteSpec(100, DataKind.Bool, 1);
        _writeSpecs["小车点动后退"] = new ParameterWriteSpec(101, DataKind.Bool, 1);
        _writeSpecs["小车定位回零"] = new ParameterWriteSpec(102, DataKind.Bool, 1);
        _writeSpecs["小车故障复位"] = new ParameterWriteSpec(109, DataKind.Bool, 1);

        // 砂轮纵向
        _writeSpecs["砂轮纵向点动速度"] = new ParameterWriteSpec(1040, DataKind.Int32, 100);
        _writeSpecs["砂轮纵向定位运行地址"] = new ParameterWriteSpec(1042, DataKind.Int32, 10000);
        _writeSpecs["砂轮纵向定位运行速度"] = new ParameterWriteSpec(1044, DataKind.Int32, 100);
        _writeSpecs["砂轮纵向定位运行启动"] = new ParameterWriteSpec(304, DataKind.Bool, 1);
        _writeSpecs["砂轮纵向点动下行"] = new ParameterWriteSpec(300, DataKind.Bool, 1);
        _writeSpecs["砂轮纵向点动上行"] = new ParameterWriteSpec(301, DataKind.Bool, 1);
        _writeSpecs["砂轮纵向定位回零"] = new ParameterWriteSpec(302, DataKind.Bool, 1);
        _writeSpecs["砂轮纵向故障复位"] = new ParameterWriteSpec(309, DataKind.Bool, 1);

        // 砂轮横向
        _writeSpecs["砂轮横向点动速度"] = new ParameterWriteSpec(1060, DataKind.Int32, 100);
        _writeSpecs["砂轮横向定位运行地址"] = new ParameterWriteSpec(1062, DataKind.Int32, 10000);
        _writeSpecs["砂轮横向定位运行速度"] = new ParameterWriteSpec(1064, DataKind.Int32, 100);
        _writeSpecs["砂轮横向定位运行启动"] = new ParameterWriteSpec(404, DataKind.Bool, 1);
        _writeSpecs["砂轮横向点动左行"] = new ParameterWriteSpec(400, DataKind.Bool, 1);
        _writeSpecs["砂轮横向点动右行"] = new ParameterWriteSpec(401, DataKind.Bool, 1);
        _writeSpecs["砂轮横向定位回零"] = new ParameterWriteSpec(402, DataKind.Bool, 1);
        _writeSpecs["砂轮横向故障复位"] = new ParameterWriteSpec(409, DataKind.Bool, 1);

        // 砂轮角度
        _writeSpecs["砂轮角度点动速度"] = new ParameterWriteSpec(1080, DataKind.Int32, 100);
        _writeSpecs["砂轮角度定位运行地址"] = new ParameterWriteSpec(1082, DataKind.Int32, 10000);
        _writeSpecs["砂轮角度定位运行速度"] = new ParameterWriteSpec(1084, DataKind.Int32, 100);
        _writeSpecs["砂轮角度定位运行启动"] = new ParameterWriteSpec(504, DataKind.Bool, 1);
        _writeSpecs["砂轮角度点动逆行"] = new ParameterWriteSpec(500, DataKind.Bool, 1);
        _writeSpecs["砂轮角度点动顺行"] = new ParameterWriteSpec(501, DataKind.Bool, 1);
        _writeSpecs["砂轮角度定位回零"] = new ParameterWriteSpec(502, DataKind.Bool, 1);
        _writeSpecs["砂轮角度故障复位"] = new ParameterWriteSpec(509, DataKind.Bool, 1);

        // 廓形仪
        _writeSpecs["廓形仪点动速度"] = new ParameterWriteSpec(1100, DataKind.Int32, 100);
        _writeSpecs["廓形仪定位运行地址"] = new ParameterWriteSpec(1102, DataKind.Int32, 10000);
        _writeSpecs["廓形仪定位运行速度"] = new ParameterWriteSpec(1104, DataKind.Int32, 100);
        _writeSpecs["廓形仪定位运行启动"] = new ParameterWriteSpec(604, DataKind.Bool, 1);
        _writeSpecs["廓形仪点动下行"] = new ParameterWriteSpec(600, DataKind.Bool, 1);
        _writeSpecs["廓形仪点动上行"] = new ParameterWriteSpec(601, DataKind.Bool, 1);
        _writeSpecs["廓形仪定位回零"] = new ParameterWriteSpec(602, DataKind.Bool, 1);
        _writeSpecs["廓形仪故障复位"] = new ParameterWriteSpec(609, DataKind.Bool, 1);

        // 砂轮旋转速度 / 质量控制
        _writeSpecs["砂轮旋转速度"] = new ParameterWriteSpec(1150, DataKind.Int16, 1.0 / 0.3);
        _writeSpecs["砂轮质量控制"] = new ParameterWriteSpec(1152, DataKind.Int16, 1);
    }

    /// <summary>
    /// 执行“修改”：解析输入、换算、写入 PLC
    /// </summary>
    private async Task ExecuteModifyAsync(MotorParameterItem item)
    {
        if (!_writeSpecs.TryGetValue(item.Name, out var spec))
        {
            _uiContext.Post(_ => ConnectionStatus = $"未找到参数映射: {item.Name}", null);
            return;
        }

        if (_plc == null || !_plc.IsConnected)
        {
            _uiContext.Post(_ => ConnectionStatus = "未连接，无法写入", null);
            return;
        }

        string input = item.InputValue?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            _uiContext.Post(_ => ConnectionStatus = "请输入有效数值/开关", null);
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
                        _uiContext.Post(_ => ConnectionStatus = "布尔输入无效，请输入 true/false 或 1/0", null);
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
                        _uiContext.Post(_ => ConnectionStatus = "数值输入无效", null);
                        return;
                    }
                    double raw = number * spec.Scale;
                    if (raw > int.MaxValue || raw < int.MinValue)
                    {
                        _uiContext.Post(_ => ConnectionStatus = "数值超出 Int32 范围", null);
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
                        _uiContext.Post(_ => ConnectionStatus = "数值输入无效", null);
                        return;
                    }
                    double raw = number * spec.Scale;
                    if (raw > short.MaxValue || raw < short.MinValue)
                    {
                        _uiContext.Post(_ => ConnectionStatus = "数值超出 Int16 范围", null);
                        return;
                    }
                    short rawShort = (short)Math.Round(raw);
                    _plc.WriteInt16(spec.Address, rawShort);
                    break;
                }
                default:
                    _uiContext.Post(_ => ConnectionStatus = "不支持的数据类型", null);
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
            _uiContext.Post(_ => ConnectionStatus = $"写入失败: {ex.Message}", null);
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

public class MotorParameterItem : INotifyPropertyChanged
{
    private string _value;
    private string _inputValue;

    /// <summary>
    /// 参数项（读/写通用）
    /// </summary>
    public MotorParameterItem(string name, bool isReadOnly)
    {
        Name = name;
        IsReadOnly = isReadOnly;
    }

    public string Name { get; }
    public bool IsReadOnly { get; }

    /// <summary>
    /// 读取值（只读参数显示）
    /// </summary>
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

    /// <summary>
    /// 输入值（写入参数编辑）
    /// </summary>
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

    /// <summary>
    /// 触发属性变化通知
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

