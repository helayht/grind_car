using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.Grinding;
using GrindCar.Services;
using GrindCar.Services.Grinding;
using GrindCar.Services.Measurement;

namespace GrindCar.ViewModels;

public abstract class GrindingObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class GrindingFieldViewModel : GrindingObservable
{
    private string _text = string.Empty;
    public GrindingParameterDefinition Definition { get; }
    public string Label { get; }
    public string Text
    {
        get => _text;
        set { if (_text == value) return; _text = value; Notify(); }
    }
    public GrindingFieldViewModel(GrindingParameterDefinition definition, int index = 0)
    {
        Definition = definition;
        Label = $"{definition.Name} ({definition.Unit})";
    }
    public decimal Parse()
    {
        if (!decimal.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out decimal value) &&
            !decimal.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            throw new InvalidOperationException($"{Label}：请输入有效数值。");
        GrindingSettingsValidator.ToRaw(Definition, value);
        return value;
    }
}

public sealed class GrindingAngleViewModel : GrindingObservable
{
    public int Number { get; }
    public int Angle { get; }
    public string AngleLabel => $"第 {Number} 组 · {Angle}°";
    public ObservableCollection<GrindingFieldViewModel> Fields { get; }
    private string _times = "未测量";
    private string _writeState = "未测量";
    public string Times { get => _times; private set { _times = value; Notify(); } }
    public string TimesWriteState { get => _writeState; private set { _writeState = value; Notify(); } }
    public GrindingAngleViewModel(int index)
    {
        Number = index + 1;
        Angle = MotorParameterDefinitions.MeasurementGrindingAngles[index];
        Fields = new(MotorParameterDefinitions.GrindingAngleParameters.Select(definition => new GrindingFieldViewModel(definition, index)));
    }
    public GrindingAngleSettings Snapshot() => new()
    {
        Number = Number, Angle = Angle, Values = Fields.ToDictionary(field => field.Definition.Key, field => field.Parse())
    };
    public void SetResult(int times, string state)
    {
        Times = times.ToString(CultureInfo.CurrentCulture);
        TimesWriteState = state;
    }
}

public sealed class GrindingParametersViewModel : GrindingObservable, IDisposable
{
    private readonly IGrindingParameterService _service;
    private readonly GrindingSettingsStore _store;
    private readonly SharedPlcConnectionService _connection;
    private readonly MainWindowMeasurementViewModel _measurement;
    private CancellationTokenSource? _pollCancellation;
    private Task _pollTask = Task.CompletedTask;
    private int _connectionGeneration;
    private bool? _running;
    private bool _busy;
    private bool _measurementActive;
    private string _saveStatus = "尚未保存";
    private string _writeStatus = "尚未写入PLC";
    private GrindingAngleViewModel _selected;
    public ObservableCollection<GrindingFieldViewModel> BasicFields { get; }
    public ObservableCollection<GrindingAngleViewModel> Angles { get; }
    public GrindingAngleViewModel SelectedAngle { get => _selected; set { if (value == null) return; _selected = value; Notify(); } }
    public string ConnectionStatus => _connection.ConnectionStatus;
    public string RunningStatus => _running.HasValue ? (_running.Value ? "打磨运行中" : "打磨已停止") : "打磨状态：未知";
    public bool CanWrite => !_busy && !_measurementActive && !_measurement.IsBusy && _connection.IsConnected && _running == false;
    public bool CanEdit => !_busy;
    public bool IsBusy => _busy;
    public string SaveStatus { get => _saveStatus; private set { _saveStatus = value; Notify(); } }
    public string WriteStatus { get => _writeStatus; private set { _writeStatus = value; Notify(); } }

    public GrindingParametersViewModel(IGrindingParameterService service, GrindingSettingsStore store,
        SharedPlcConnectionService connection, MainWindowMeasurementViewModel measurement)
    {
        _service = service; _store = store; _connection = connection; _measurement = measurement;
        BasicFields = new(MotorParameterDefinitions.GrindingBasicParameters.Select(definition => new GrindingFieldViewModel(definition)));
        Angles = new(Enumerable.Range(0, MotorParameterDefinitions.MeasurementGrindingAngles.Count).Select(index => new GrindingAngleViewModel(index)));
        _selected = Angles[0];
        foreach (GrindingFieldViewModel field in BasicFields.Concat(Angles.SelectMany(angle => angle.Fields)))
            field.PropertyChanged += FieldChanged;
        _connection.PropertyChanged += ConnectionChanged;
        _measurement.PropertyChanged += MeasurementChanged;
        try
        {
            GrindingSettings? settings = _store.Load();
            if (settings != null)
            {
                foreach (GrindingFieldViewModel field in BasicFields) field.Text = settings.Basic[field.Definition.Key].ToString(CultureInfo.CurrentCulture);
                foreach (GrindingAngleViewModel angle in Angles)
                    foreach (GrindingFieldViewModel field in angle.Fields)
                        field.Text = settings.Angles[angle.Number - 1].Values[field.Definition.Key].ToString(CultureInfo.CurrentCulture);
                SaveStatus = "已加载本地配置（未写入PLC）";
            }
        }
        catch (Exception ex) { SaveStatus = ex.Message; }
    }

    public GrindingSettings Snapshot() => new()
    {
        Basic = BasicFields.ToDictionary(field => field.Definition.Key, field => field.Parse()),
        Angles = Angles.Select(angle => angle.Snapshot()).ToList()
    };

    public void Save()
    {
        try { _store.Save(Snapshot()); SaveStatus = "本地保存成功"; }
        catch (Exception ex) { SaveStatus = $"保存失败：{ex.Message}"; }
    }

    public async Task WriteAsync(bool selectedOnly)
    {
        if (!CanWrite) { WriteStatus = "请先连接PLC，确认打磨已停止且测量流程未运行。"; return; }
        _busy = true;
        _measurement.SetGrindingParametersBusy(true);
        NotifyOperation();
        try
        {
            WriteStatus = "正在写入PLC…";
            if (selectedOnly) await _service.WriteAngleAsync(SelectedAngle.Snapshot(), CancellationToken.None);
            else await _service.WriteAllAsync(Snapshot(), CancellationToken.None);
            WriteStatus = selectedOnly ? $"第 {SelectedAngle.Number} 组参数写入成功（不含次数）" : "全部打磨参数写入成功（不含次数）";
        }
        catch (Exception ex) { WriteStatus = ex.Message; SetRunning(null); }
        finally { _busy = false; _measurement.SetGrindingParametersBusy(false); NotifyOperation(); }
    }

    public void SetConfirmedResults(IReadOnlyList<MeasurementGrindingTimesResult> results, string state)
    {
        foreach (MeasurementGrindingTimesResult result in results)
            Angles.Single(angle => angle.Angle == result.Angle).SetResult(result.GrindingTimes, state);
    }

    public void SetMeasurementActive(bool active)
    {
        _measurementActive = active;
        Notify(nameof(CanWrite));
    }

    public void StartPolling()
    {
        if (_pollCancellation != null) return;
        var cancellation = new CancellationTokenSource();
        _pollCancellation = cancellation;
        _pollTask = PollAsync(cancellation.Token);
    }

    public async Task StopPollingAsync()
    {
        CancellationTokenSource? cancellation = _pollCancellation;
        if (cancellation == null) return;
        cancellation.Cancel();
        SetRunning(null);
        await _pollTask;
        cancellation.Dispose();
        if (ReferenceEquals(_pollCancellation, cancellation)) _pollCancellation = null;
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!_connection.IsConnected) SetRunning(null);
                else if (!_busy)
                {
                    int generation = _connectionGeneration;
                    try
                    {
                        bool running = await _service.ReadRunningAsync(token);
                        if (!token.IsCancellationRequested && generation == _connectionGeneration && _connection.IsConnected)
                            SetRunning(running);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception) { SetRunning(null); }
                }
                await Task.Delay(MotorParameterDefinitions.GrindingStatusPollIntervalMs, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void SetRunning(bool? value) { _running = value; Notify(nameof(RunningStatus)); Notify(nameof(CanWrite)); }
    private void NotifyOperation() { Notify(nameof(IsBusy)); Notify(nameof(CanEdit)); Notify(nameof(CanWrite)); }
    private void FieldChanged(object? sender, PropertyChangedEventArgs e) { SaveStatus = "有未保存的修改"; WriteStatus = "参数已修改，尚未写入PLC"; }
    private void ConnectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        Interlocked.Increment(ref _connectionGeneration);
        SetRunning(null);
        Notify(nameof(ConnectionStatus));
    }
    private void MeasurementChanged(object? sender, PropertyChangedEventArgs e) => Notify(nameof(CanWrite));
    public void Dispose()
    {
        _pollCancellation?.Cancel();
        _connection.PropertyChanged -= ConnectionChanged;
        _measurement.PropertyChanged -= MeasurementChanged;
    }
}
