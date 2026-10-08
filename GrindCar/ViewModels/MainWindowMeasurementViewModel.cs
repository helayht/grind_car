using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GrindCar.Definitions;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Measurement;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Measurement;
using GrindCar.Services.PointCloud;

namespace GrindCar.ViewModels;

/// <summary>
/// 主窗口测量参数区域 ViewModel。
/// </summary>
public class MainWindowMeasurementViewModel : INotifyPropertyChanged, IDisposable
{
    private const string CarSpeedParameterName = MotorParameterDefinitions.CarMeasurementSpeedName;
    private const string ProfileCountParameterName = "单次测量总条数";

    private readonly IMeasurementParameterService _measurementParameterService;
    private readonly SharedPlcConnectionService _plcConnection;
    private readonly PointCloudCaptureSettingsStore _pointCloudCaptureSettingsStore;
    private readonly MeasurementPositionSettingsStore _positionSettingsStore;
    public DashboardTelemetryViewModel Telemetry { get; }
    private string _profilerPositionText = string.Empty;
    private string _avoidancePositionText = string.Empty;
    private string _positionSaveStatus = "测量位置参数尚未保存。";
    private bool _workflowActive;
    public string PositionSaveStatus { get => _positionSaveStatus; private set { _positionSaveStatus = value; OnPropertyChanged(); } }
    public string ProfilerPositionText { get => _profilerPositionText; set { _profilerPositionText = value; OnPropertyChanged(); PositionSaveStatus = "测量位置参数有未保存修改。"; } }
    public string AvoidancePositionText { get => _avoidancePositionText; set { _avoidancePositionText = value; OnPropertyChanged(); PositionSaveStatus = "测量位置参数有未保存修改。"; } }

    private string _plcIpAddress;
    private int _plcPort;
    private string _startPositionText = string.Empty;
    private string _endPositionText = string.Empty;
    private string _carSpeedText = string.Empty;
    private string _profileCountText = string.Empty;
    private string _statusMessage = "请填写参数后写入。";
    private bool _isBusy;
    private RailProfileType _selectedProfileType = RailProfileType.Kg60;

    public IReadOnlyList<RailProfileType> ProfileTypeOptions { get; } = new List<RailProfileType>
    {
        RailProfileType.Kg60,
        RailProfileType.Kg50
    };

    public RailProfileType SelectedProfileType
    {
        get => _selectedProfileType;
        set
        {
            if (_selectedProfileType == value)
            {
                return;
            }

            _selectedProfileType = value;
            RailSurfaceService.SwitchProfile(value);
            string profileName = GetProfileTypeDisplayName(value);
            StatusMessage = $"已切换为标准轨面型号：{profileName}";
            OnPropertyChanged();
        }
    }

    public MainWindowMeasurementViewModel(
        IMeasurementParameterService measurementParameterService,
        SharedPlcConnectionService plcConnection,
        string defaultIpAddress,
        int defaultPort,
        PointCloudCaptureSettingsStore? pointCloudCaptureSettingsStore = null,
        MeasurementPositionSettingsStore? positionSettingsStore = null,
        DashboardTelemetryViewModel? telemetry = null)
    {
        _measurementParameterService = measurementParameterService ?? throw new ArgumentNullException(nameof(measurementParameterService));
        _plcConnection = plcConnection ?? throw new ArgumentNullException(nameof(plcConnection));
        _pointCloudCaptureSettingsStore = pointCloudCaptureSettingsStore ?? new PointCloudCaptureSettingsStore();
        _positionSettingsStore = positionSettingsStore ?? new MeasurementPositionSettingsStore();
        Telemetry = telemetry ?? new DashboardTelemetryViewModel(plcConnection);
        Telemetry.PropertyChanged += TelemetryChanged;
        _plcIpAddress = defaultIpAddress;
        _plcPort = defaultPort;
        _plcConnection.PropertyChanged += PlcConnection_PropertyChanged;
        LoadPointCloudCaptureSettings();
        try
        {
            MeasurementPositions? positions = _positionSettingsStore.Load();
            if (positions != null)
            {
                _startPositionText = positions.Start.ToString("R", CultureInfo.CurrentCulture);
                _endPositionText = positions.End.ToString("R", CultureInfo.CurrentCulture);
                _profilerPositionText = positions.ProfilerPosition.ToString("R", CultureInfo.CurrentCulture);
                _avoidancePositionText = positions.AvoidancePosition.ToString("R", CultureInfo.CurrentCulture);
                PositionSaveStatus = "已恢复本地测量位置参数，未写入PLC。";
            }
        }
        catch (Exception ex) { PositionSaveStatus = ex.Message; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? MeasurementEnded;

    public string EndpointText => $"{_plcIpAddress}:{_plcPort}";

    public string PlcIpAddress => _plcIpAddress;

    public int PlcPort => _plcPort;

    public string PlcConnectionStatus => _plcConnection.ConnectionStatus;

    public string StartPositionText
    {
        get => _startPositionText;
        set
        {
            if (_startPositionText == value)
            {
                return;
            }

            _startPositionText = value;
            PositionSaveStatus = "测量位置参数有未保存修改。";
            OnPropertyChanged();
        }
    }

    public string EndPositionText
    {
        get => _endPositionText;
        set
        {
            if (_endPositionText == value)
            {
                return;
            }

            _endPositionText = value;
            PositionSaveStatus = "测量位置参数有未保存修改。";
            OnPropertyChanged();
        }
    }


    public string CarSpeedText
    {
        get => _carSpeedText;
        set
        {
            if (_carSpeedText == value)
            {
                return;
            }

            _carSpeedText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CalculatedFrameRateText));
        }
    }

    public string ProfileCountText
    {
        get => _profileCountText;
        set
        {
            if (_profileCountText == value)
            {
                return;
            }

            _profileCountText = value;
            OnPropertyChanged();
        }
    }

    public string CalculatedFrameRateText
    {
        get
        {
            try
            {
                double speedMetersPerMinute = MeasurementInputParser.ParsePositiveDouble(CarSpeedText, CarSpeedParameterName);
                double frameRateHz = PointCloudCaptureSettings.CalculateFrameRateHz(speedMetersPerMinute);
                return $"{frameRateHz.ToString("0.###", CultureInfo.CurrentCulture)} Hz";
            }
            catch
            {
                return "-- Hz";
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanWriteMeasurementParameters));
        }
    }

    private bool _grindingParametersBusy;
    public bool CanOperate => !IsBusy && !_grindingParametersBusy && !_workflowActive;
    public bool CanWriteMeasurementParameters => CanOperate && _plcConnection.IsConnected && Telemetry.MeasurementRunning == false;

    public void SetWorkflowActive(bool active)
    {
        _workflowActive = active;
        OnPropertyChanged(nameof(CanOperate));
        OnPropertyChanged(nameof(CanWriteMeasurementParameters));
    }

    public void SetGrindingParametersBusy(bool busy)
    {
        _grindingParametersBusy = busy;
        OnPropertyChanged(nameof(CanOperate));
        OnPropertyChanged(nameof(CanWriteMeasurementParameters));
    }

    public async Task ConnectPlcAsync(string ipAddress, int port)
    {
        try
        {
            IsBusy = true;
            StatusMessage = $"正在连接 PLC：{ipAddress}:{port}";
            await _plcConnection.ConnectAsync(ipAddress, port).ConfigureAwait(true);
            _plcIpAddress = _plcConnection.IpAddress;
            _plcPort = _plcConnection.Port;
            NotifyEndpointChanged();
            StatusMessage = $"PLC连接成功：{EndpointText}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task WriteMeasurementParametersAsync()
    {
        if (!CanWriteMeasurementParameters)
            throw new InvalidOperationException("请连接PLC并确认测量已停止，等待当前操作完成后写入。");
        var parameters = new MeasurementParameters(CreatePositions(),
            MeasurementInputParser.ParsePositiveDouble(CarSpeedText, CarSpeedParameterName));

        try
        {
            IsBusy = true;
            StatusMessage = "正在写入测量参数...";

            await _measurementParameterService.WriteMeasurementParametersAsync(
                _plcIpAddress,
                _plcPort,
                parameters).ConfigureAwait(true);

            StatusMessage = "五项测量参数写入成功（起止点、测量位置、回避位、测量行走速度）。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SavePointCloudCaptureSettings()
    {
        PointCloudCaptureSettings settings = CreatePointCloudCaptureSettingsFromInput();
        _pointCloudCaptureSettingsStore.Save(settings);
        StatusMessage =
            $"点云采集参数已保存：速度 {settings.SpeedMetersPerMinute.ToString("0.###", CultureInfo.CurrentCulture)} m/min，单次 {settings.ProfileCount.ToString(CultureInfo.CurrentCulture)} 条，帧率 {settings.FrameRateHz.ToString("0.###", CultureInfo.CurrentCulture)} Hz";
    }

    private MeasurementPositions CreatePositions() => new(
        MeasurementInputParser.ParsePosition(StartPositionText, MotorParameterDefinitions.MeasurementStartPositionName),
        MeasurementInputParser.ParsePosition(EndPositionText, MotorParameterDefinitions.MeasurementEndPositionName),
        MeasurementInputParser.ParsePosition(ProfilerPositionText, MotorParameterDefinitions.ProfilerMeasurementPositionName),
        MeasurementInputParser.ParsePosition(AvoidancePositionText, MotorParameterDefinitions.ProfilerAvoidancePositionName));

    public void SaveMeasurementPositions()
    {
        try { _positionSettingsStore.Save(CreatePositions()); PositionSaveStatus = "测量位置参数已保存到本地，未写入PLC。"; }
        catch (Exception ex) { PositionSaveStatus = $"保存失败：{ex.Message}"; }
    }


    public async Task<MeasurementGrindingWorkflowResult> StartMeasurementMotionAsync()
    {
        try
        {
            IsBusy = true;
            PointCloudCaptureSettings captureSettings = CreatePointCloudCaptureSettingsFromInput();
            _pointCloudCaptureSettingsStore.Save(captureSettings);
            StatusMessage =
                $"点云采集参数已保存，帧率 {captureSettings.FrameRateHz.ToString("0.###", CultureInfo.CurrentCulture)} Hz，正在启动测量流程...";

            var progress = new Progress<string>(message => StatusMessage = message);
            SynchronizationContext? uiContext = SynchronizationContext.Current;
            MeasurementGrindingWorkflowResult result =
                await _measurementParameterService.RunMeasurementWorkflowAsync(
                    _plcIpAddress,
                    _plcPort,
                    progress,
                    () => NotifyMeasurementEnded(uiContext)).ConfigureAwait(true);

            StatusMessage =
                $"测量流程完成：累计 {result.SampleCount.ToString(CultureInfo.CurrentCulture)} 次测量，已生成 {result.Results.Count.ToString(CultureInfo.CurrentCulture)} 个角度的打磨深度。";
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task WriteGrindingTimesAsync(IReadOnlyList<MeasurementGrindingTimesResult> results)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "正在写入确认后的打磨次数...";

            await _measurementParameterService.WriteGrindingTimesAsync(
                _plcIpAddress,
                _plcPort,
                results).ConfigureAwait(true);

            StatusMessage =
                $"打磨次数已写入 PLC，共写入 {results.Count.ToString(CultureInfo.CurrentCulture)} 个角度。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetMeasurementWriteCanceled()
    {
        StatusMessage = "已取消写入 PLC。";
    }

    public async Task StartGrindingMotionAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "正在启动打磨运动...";

            await _measurementParameterService.StartGrindingMotionAsync(
                _plcIpAddress,
                _plcPort).ConfigureAwait(true);

            StatusMessage = "打磨运动启动信号已写入。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetErrorStatus(string message)
    {
        StatusMessage = message;
    }

    private static string GetProfileTypeDisplayName(RailProfileType type)
    {
        return type switch
        {
            RailProfileType.Kg60 => "60kg/m",
            RailProfileType.Kg50 => "50kg/m",
            _ => type.ToString()
        };
    }

    private PointCloudCaptureSettings CreatePointCloudCaptureSettingsFromInput()
    {
        double speedMetersPerMinute = MeasurementInputParser.ParsePositiveDouble(CarSpeedText, CarSpeedParameterName);
        int profileCount = MeasurementInputParser.ParsePositiveInt32(ProfileCountText, ProfileCountParameterName);
        return new PointCloudCaptureSettings(speedMetersPerMinute, profileCount);
    }

    private void LoadPointCloudCaptureSettings()
    {
        try
        {
            PointCloudCaptureSettings? settings = _pointCloudCaptureSettingsStore.Load();
            if (settings == null)
            {
                return;
            }

            _carSpeedText = settings.SpeedMetersPerMinute.ToString("0.###", CultureInfo.CurrentCulture);
            _profileCountText = settings.ProfileCount.ToString(CultureInfo.CurrentCulture);
            _statusMessage =
                $"已加载点云采集参数：速度 {_carSpeedText} m/min，单次 {_profileCountText} 条。";
        }
        catch (Exception ex)
        {
            _statusMessage = $"点云采集参数加载失败：{ex.Message}";
        }
    }

    private void NotifyMeasurementEnded(SynchronizationContext? uiContext)
    {
        if (uiContext == null)
        {
            MeasurementEnded?.Invoke();
            return;
        }

        uiContext.Post(_ => MeasurementEnded?.Invoke(), null);
    }

    private void PlcConnection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanWriteMeasurementParameters));
        if (e.PropertyName == nameof(SharedPlcConnectionService.ConnectionStatus))
        {
            OnPropertyChanged(nameof(PlcConnectionStatus));
        }
    }

    private void TelemetryChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(CanWriteMeasurementParameters));

    public void Dispose()
    {
        Telemetry.PropertyChanged -= TelemetryChanged;
        Telemetry.Dispose();
        _plcConnection.PropertyChanged -= PlcConnection_PropertyChanged;
    }

    private void NotifyEndpointChanged()
    {
        OnPropertyChanged(nameof(EndpointText));
        OnPropertyChanged(nameof(PlcIpAddress));
        OnPropertyChanged(nameof(PlcPort));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
