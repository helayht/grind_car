using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using GrindCar.Models.Measurement;
using GrindCar.Models.Rail;
using GrindCar.Services.Measurement;
using GrindCar.Services;
using GrindCar.Services.Rail.Debug;

namespace GrindCar.ViewModels;

public sealed record ProfileAxisLabel(double Left, double Top, double Width, string Text);

/// <summary>首页只保留最新有效组。所有异步通知按 UI 队列顺序执行，并核对测量批次。</summary>
public sealed class DashboardProfileViewModel : INotifyPropertyChanged
{
    private const int HorizontalIntervals = 4;
    private const int VerticalIntervals = 2;
    private readonly SynchronizationContext? _context;
    private readonly Action<string> _log;
    private readonly RepresentativeProfileComparisonService _service = new();
    private RepresentativeProfileComparisonSnapshot? _snapshot;
    private RepresentativeProfileComparisonSnapshot? _standardSnapshot;
    private long _runId;
    private bool _accepting;
    private double _width;
    private double _height;
    private string _plotError = string.Empty;
    private string _stateText = "等待测量数据";

    public DashboardProfileViewModel(Action<string> log, SynchronizationContext? context = null)
    {
        _log = log;
        _context = context ?? SynchronizationContext.Current;
        Reset(RailSurfaceService.CurrentProfileType);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public RepresentativeProfileBounds? AccumulatedBounds { get; private set; }
    public RepresentativeProfilePlotResult Plot { get; private set; } = RepresentativeProfilePlotResult.Empty;
    public Geometry GridGeometry { get; private set; } = Geometry.Empty;
    public IReadOnlyList<ProfileAxisLabel> AxisLabels { get; private set; } = Array.Empty<ProfileAxisLabel>();
    public int? SampleIndex { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string ProfileTypeText { get; private set; } = "60 kg/m";
    public string MetadataText => SampleIndex.HasValue
        ? $"{ProfileTypeText} · 第 {SampleIndex} 组 · {UpdatedAt:HH:mm:ss}"
        : $"{ProfileTypeText} · 尚无完整测量组";
    public string StatusText => string.IsNullOrEmpty(_plotError) ? _stateText : $"{_stateText}；{_plotError}";
    public string EmptyText => string.IsNullOrEmpty(_plotError) ? "等待测量数据" : "廓形显示失败，请查看运行消息";
    public Visibility EmptyVisibility => _snapshot == null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>从 UI 线程开始新测量，返回用于过滤后续异步通知的批次号。</summary>
    public long Begin(RailProfileType type)
    {
        Reset(type);
        _accepting = true;
        _stateText = "等待第一组左右测量完成";
        Notify();
        return _runId;
    }

    public void Reset(RailProfileType type)
    {
        _runId++;
        _accepting = false;
        _snapshot = null;
        AccumulatedBounds = null;
        SampleIndex = null;
        UpdatedAt = null;
        Plot = RepresentativeProfilePlotResult.Empty;
        GridGeometry = Geometry.Empty;
        AxisLabels = Array.Empty<ProfileAxisLabel>();
        ProfileTypeText = type == RailProfileType.Kg50 ? "50 kg/m" : "60 kg/m";
        _plotError = string.Empty;
        _stateText = "等待测量数据";
        try
        {
            _standardSnapshot = _service.BuildStandardSnapshot();
            AccumulatedBounds = _standardSnapshot.Bounds;
            ApplyPlot(_service.BuildPlot(_standardSnapshot, _width, _height,
                RepresentativeProfileCurveStyle.Smooth, AccumulatedBounds));
        }
        catch (Exception ex) { _standardSnapshot = null; ShowError($"标准廓形显示失败：{ex.Message}"); }
        Notify();
    }

    public IProgress<MeasurementRepresentativeProfile> CreateProgress(long runId) =>
        new ProfileProgress(profile =>
        {
            DateTime receivedAt = DateTime.Now;
            Post(() =>
            {
                if (runId == _runId && _accepting) Receive(profile, receivedAt);
            });
        });

    public void SetStage(long runId, MeasurementWorkflowStage stage) => Post(() =>
    {
        if (runId != _runId || !_accepting) return;
        _stateText = stage == MeasurementWorkflowStage.Calculating ? "正在计算本组或汇总结果" :
            SampleIndex.HasValue ? "保留已完成组，等待下一组左右测量完成" : "等待第一组左右测量完成";
        Notify();
    });

    /// <summary>结束标记也排入同一队列，使已发布的最后一组先显示，之后拒绝迟到通知。</summary>
    public void End(long runId, bool failed) => Post(() =>
    {
        if (runId != _runId) return;
        _accepting = false;
        _stateText = failed ? "测量中断" : "测量结束，保留最后有效组";
        Notify();
    });

    private void Receive(MeasurementRepresentativeProfile profile, DateTime receivedAt)
    {
        if (SampleIndex.HasValue && profile.SampleIndex <= SampleIndex.Value) return;
        try
        {
            if (profile.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                throw new InvalidOperationException("代表点含非有限坐标。");
            RepresentativeProfileComparisonSnapshot snapshot = _service.BuildSnapshot(profile.Points);
            if (_standardSnapshot != null) snapshot = snapshot with { StandardPoints = _standardSnapshot.StandardPoints };
            RepresentativeProfileBounds bounds = AccumulatedBounds is { } previous
                ? new(Math.Min(previous.MinX, snapshot.Bounds.MinX), Math.Max(previous.MaxX, snapshot.Bounds.MaxX),
                    Math.Min(previous.MinY, snapshot.Bounds.MinY), Math.Max(previous.MaxY, snapshot.Bounds.MaxY))
                : snapshot.Bounds;
            RepresentativeProfilePlotResult plot = _service.BuildPlot(snapshot, _width, _height,
                RepresentativeProfileCurveStyle.Smooth, bounds);
            ApplyPlot(plot);
            _snapshot = snapshot;
            AccumulatedBounds = bounds;
            SampleIndex = profile.SampleIndex;
            UpdatedAt = receivedAt;
            _plotError = string.Empty;
            _stateText = "已更新，等待下一组左右测量完成";
            Notify();
        }
        catch (Exception ex) { ShowError($"第 {profile.SampleIndex} 组显示失败：{ex.Message}"); }
    }

    public void Resize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        _width = width;
        _height = height;
        RepresentativeProfileComparisonSnapshot? visibleSnapshot = _snapshot ?? _standardSnapshot;
        if (visibleSnapshot == null) return;
        try
        {
            ApplyPlot(_service.BuildPlot(visibleSnapshot, width, height, RepresentativeProfileCurveStyle.Smooth, AccumulatedBounds));
            Notify();
        }
        catch (Exception ex) { ShowError($"廓形尺寸更新失败：{ex.Message}"); }
    }

    private void ApplyPlot(RepresentativeProfilePlotResult plot)
    {
        var labels = new List<ProfileAxisLabel>();
        var grid = new StreamGeometry();
        if (plot.Bounds is { } bounds)
        {
            using StreamGeometryContext context = grid.Open();
            for (int index = 0; index <= HorizontalIntervals; index++)
            {
                double fraction = (double)index / HorizontalIntervals;
                double x = plot.XAxisX1 + fraction * (plot.XAxisX2 - plot.XAxisX1);
                context.BeginFigure(new Point(x, plot.YAxisY1), false, false);
                context.LineTo(new Point(x, plot.YAxisY2), true, false);
                labels.Add(new ProfileAxisLabel(x - 27, plot.YAxisY2 + 3, 54,
                    Format(bounds.MinX + fraction * (bounds.MaxX - bounds.MinX))));
            }
            for (int index = 0; index <= VerticalIntervals; index++)
            {
                double fraction = (double)index / VerticalIntervals;
                double y = plot.YAxisY2 - fraction * (plot.YAxisY2 - plot.YAxisY1);
                context.BeginFigure(new Point(plot.XAxisX1, y), false, false);
                context.LineTo(new Point(plot.XAxisX2, y), true, false);
                labels.Add(new ProfileAxisLabel(0, y - 7, 32,
                    Format(bounds.MinY + fraction * (bounds.MaxY - bounds.MinY))));
            }
        }
        grid.Freeze();
        Plot = plot;
        GridGeometry = grid;
        AxisLabels = labels;
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private void ShowError(string message)
    {
        if (_plotError != message) _log(message);
        _plotError = message;
        Notify();
    }

    private void Post(Action action)
    {
        if (_context == null) action();
        else _context.Post(_ => action(), null);
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    private sealed class ProfileProgress : IProgress<MeasurementRepresentativeProfile>
    {
        private readonly Action<MeasurementRepresentativeProfile> _report;
        public ProfileProgress(Action<MeasurementRepresentativeProfile> report) => _report = report;
        public void Report(MeasurementRepresentativeProfile value) => _report(value);
    }
}
