using System;
using System.Globalization;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GrindCar.Definitions;
using GrindCar.Services.Measurement;

namespace GrindCar.ViewModels;

/// <summary>
/// 主窗口测量参数区域 ViewModel。
/// </summary>
public class MainWindowMeasurementViewModel : INotifyPropertyChanged
{
    private readonly IMeasurementParameterService _measurementParameterService;

    private string _plcIpAddress;
    private int _plcPort;
    private string _startPositionText = string.Empty;
    private string _endPositionText = string.Empty;
    private string _statusMessage = "请填写参数后写入。";
    private bool _isBusy;

    public MainWindowMeasurementViewModel(
        IMeasurementParameterService measurementParameterService,
        string defaultIpAddress,
        int defaultPort)
    {
        _measurementParameterService = measurementParameterService ?? throw new ArgumentNullException(nameof(measurementParameterService));
        _plcIpAddress = defaultIpAddress;
        _plcPort = defaultPort;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EndpointText => $"{_plcIpAddress}:{_plcPort}";

    public string PlcIpAddress => _plcIpAddress;

    public int PlcPort => _plcPort;

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
            OnPropertyChanged();
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
        }
    }

    public bool CanOperate => !IsBusy;

    public void UpdateEndpoint(string ipAddress, int port)
    {
        _plcIpAddress = ipAddress;
        _plcPort = port;
        OnPropertyChanged(nameof(EndpointText));
        OnPropertyChanged(nameof(PlcIpAddress));
        OnPropertyChanged(nameof(PlcPort));
        StatusMessage = $"PLC连接参数已更新：{EndpointText}";
    }

    public async Task WriteMeasurementParametersAsync()
    {
        double startPosition = MeasurementInputParser.ParsePosition(
            StartPositionText,
            MotorParameterDefinitions.MeasurementStartPositionName);
        double endPosition = MeasurementInputParser.ParsePosition(
            EndPositionText,
            MotorParameterDefinitions.MeasurementEndPositionName);

        try
        {
            IsBusy = true;
            StatusMessage = "正在写入测量参数...";

            await _measurementParameterService.WriteMeasurementRangeAsync(
                _plcIpAddress,
                _plcPort,
                startPosition,
                endPosition).ConfigureAwait(true);

            StatusMessage = $"写入成功：起点 {startPosition.ToString("0.###", CultureInfo.CurrentCulture)}m，终点 {endPosition.ToString("0.###", CultureInfo.CurrentCulture)}m";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StartMeasurementMotionAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "正在发送测量运动启动信号...";

            await _measurementParameterService.StartMeasurementMotionAsync(
                _plcIpAddress,
                _plcPort).ConfigureAwait(true);

            StatusMessage = "测量运动启动信号已发送。";
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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
