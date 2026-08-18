using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;

namespace GrindCar.ViewModels;

/// <summary>
/// 代表轨面与标准轨面对比窗口 ViewModel。
/// </summary>
public sealed class RepresentativeProfileComparisonViewModel : INotifyPropertyChanged
{
    private readonly RepresentativeProfileComparisonService _comparisonService = new();
    private readonly RepresentativeProfileComparisonSnapshot? _snapshot;
    private readonly MaximumDropProfileComparisonSnapshot? _maximumDropSnapshot;
    private readonly RepresentativeProfileCurveStyle _curveStyle;

    private Geometry _representativeCurveGeometry = Geometry.Empty;
    private Geometry _leftCurveGeometry = Geometry.Empty;
    private Geometry _rightCurveGeometry = Geometry.Empty;
    private Geometry _standardCurveGeometry = Geometry.Empty;
    private double _xAxisX1;
    private double _xAxisX2;
    private double _xAxisY1;
    private double _xAxisY2;
    private double _yAxisX1;
    private double _yAxisX2;
    private double _yAxisY1;
    private double _yAxisY2;

    public RepresentativeProfileComparisonViewModel(
        IReadOnlyList<RailProfilePoint> representativePoints,
        RepresentativeProfileCurveStyle curveStyle = RepresentativeProfileCurveStyle.Smooth)
        : this(
            new IReadOnlyList<RailProfilePoint>[] { representativePoints },
            curveStyle)
    {
    }

    public RepresentativeProfileComparisonViewModel(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> representativeSegments,
        RepresentativeProfileCurveStyle curveStyle)
    {
        _curveStyle = curveStyle;
        _snapshot = _comparisonService.BuildSnapshot(representativeSegments);
        _maximumDropSnapshot = null;
        PointCountText = _snapshot.PointCountText;
        PointCountLabelText = "代表点数量";
        XRangeText = _snapshot.XRangeText;
        YRangeText = _snapshot.YRangeText;
        HeaderText = curveStyle == RepresentativeProfileCurveStyle.Polyline
            ? "最大掉块廓形与标准轨面对比"
            : "代表轨面与标准轨面曲线对比";
        CurveDescriptionText = curveStyle == RepresentativeProfileCurveStyle.Polyline
            ? "绿色为最大掉块原始折线，橙色为标准轨面曲线。"
            : "绿色为代表拟合曲线，橙色为标准轨面曲线。";
        LeftStatusText = string.Empty;
        RightStatusText = string.Empty;
        MaximumDropLegendVisibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 初始化 Left/Right 最大掉块廓形同图对比。
    /// </summary>
    public RepresentativeProfileComparisonViewModel(
        MaximumDropProfileResult? leftMaximumDropProfile,
        MaximumDropProfileResult? rightMaximumDropProfile)
    {
        _curveStyle = RepresentativeProfileCurveStyle.Polyline;
        _snapshot = null;
        _maximumDropSnapshot = _comparisonService.BuildMaximumDropSnapshot(
            leftMaximumDropProfile,
            rightMaximumDropProfile);
        PointCountText = _maximumDropSnapshot.PointCountText;
        PointCountLabelText = "掉块廓形点数";
        XRangeText = _maximumDropSnapshot.XRangeText;
        YRangeText = _maximumDropSnapshot.YRangeText;
        HeaderText = "Left/Right 最大掉块廓形与标准轨面对比";
        CurveDescriptionText = "蓝色为 Left 原始折线，绿色为 Right 原始折线，橙色为标准轨面曲线。";
        LeftStatusText = BuildMaximumDropStatus("Left", leftMaximumDropProfile);
        RightStatusText = BuildMaximumDropStatus("Right", rightMaximumDropProfile);
        MaximumDropLegendVisibility = Visibility.Visible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RepresentativeProfileScreenPoint> RepresentativePointItems { get; } = new();

    public ObservableCollection<RepresentativeProfileScreenPoint> LeftPointItems { get; } = new();

    public ObservableCollection<RepresentativeProfileScreenPoint> RightPointItems { get; } = new();

    public string PointCountLabelText { get; }

    public string PointCountText { get; }

    public string XRangeText { get; }

    public string YRangeText { get; }

    public string HeaderText { get; }

    public string CurveDescriptionText { get; }

    public string LeftStatusText { get; }

    public string RightStatusText { get; }

    public Visibility MaximumDropLegendVisibility { get; }

    public Geometry RepresentativeCurveGeometry
    {
        get => _representativeCurveGeometry;
        private set
        {
            if (Equals(_representativeCurveGeometry, value))
            {
                return;
            }

            _representativeCurveGeometry = value;
            OnPropertyChanged();
        }
    }

    public Geometry StandardCurveGeometry
    {
        get => _standardCurveGeometry;
        private set
        {
            if (Equals(_standardCurveGeometry, value))
            {
                return;
            }

            _standardCurveGeometry = value;
            OnPropertyChanged();
        }
    }

    public Geometry LeftCurveGeometry
    {
        get => _leftCurveGeometry;
        private set
        {
            if (Equals(_leftCurveGeometry, value))
            {
                return;
            }

            _leftCurveGeometry = value;
            OnPropertyChanged();
        }
    }

    public Geometry RightCurveGeometry
    {
        get => _rightCurveGeometry;
        private set
        {
            if (Equals(_rightCurveGeometry, value))
            {
                return;
            }

            _rightCurveGeometry = value;
            OnPropertyChanged();
        }
    }

    public double XAxisX1
    {
        get => _xAxisX1;
        private set => SetField(ref _xAxisX1, value);
    }

    public double XAxisX2
    {
        get => _xAxisX2;
        private set => SetField(ref _xAxisX2, value);
    }

    public double XAxisY1
    {
        get => _xAxisY1;
        private set => SetField(ref _xAxisY1, value);
    }

    public double XAxisY2
    {
        get => _xAxisY2;
        private set => SetField(ref _xAxisY2, value);
    }

    public double YAxisX1
    {
        get => _yAxisX1;
        private set => SetField(ref _yAxisX1, value);
    }

    public double YAxisX2
    {
        get => _yAxisX2;
        private set => SetField(ref _yAxisX2, value);
    }

    public double YAxisY1
    {
        get => _yAxisY1;
        private set => SetField(ref _yAxisY1, value);
    }

    public double YAxisY2
    {
        get => _yAxisY2;
        private set => SetField(ref _yAxisY2, value);
    }

    /// <summary>
    /// 按当前画布尺寸刷新曲线和坐标轴绘图数据。
    /// </summary>
    /// <param name="plotWidth">绘图区宽度。</param>
    /// <param name="plotHeight">绘图区高度。</param>
    public void UpdatePlot(double plotWidth, double plotHeight)
    {
        if (_maximumDropSnapshot != null)
        {
            UpdateMaximumDropPlot(plotWidth, plotHeight);
            return;
        }

        RepresentativeProfilePlotResult plot = _comparisonService.BuildPlot(
            _snapshot!,
            plotWidth,
            plotHeight,
            _curveStyle);

        RepresentativeCurveGeometry = plot.RepresentativeCurveGeometry;
        LeftCurveGeometry = Geometry.Empty;
        RightCurveGeometry = Geometry.Empty;
        StandardCurveGeometry = plot.StandardCurveGeometry;

        RepresentativePointItems.Clear();
        for (int index = 0; index < plot.RepresentativePoints.Count; index++)
        {
            RepresentativePointItems.Add(plot.RepresentativePoints[index]);
        }

        LeftPointItems.Clear();
        RightPointItems.Clear();

        UpdateAxes(plot);
    }

    private void UpdateMaximumDropPlot(double plotWidth, double plotHeight)
    {
        MaximumDropProfilePlotResult plot = _comparisonService.BuildMaximumDropPlot(
            _maximumDropSnapshot!,
            plotWidth,
            plotHeight);

        RepresentativeCurveGeometry = Geometry.Empty;
        LeftCurveGeometry = plot.LeftCurveGeometry;
        RightCurveGeometry = plot.RightCurveGeometry;
        StandardCurveGeometry = plot.StandardCurveGeometry;
        RepresentativePointItems.Clear();

        LeftPointItems.Clear();
        for (int index = 0; index < plot.LeftPoints.Count; index++)
        {
            LeftPointItems.Add(plot.LeftPoints[index]);
        }

        RightPointItems.Clear();
        for (int index = 0; index < plot.RightPoints.Count; index++)
        {
            RightPointItems.Add(plot.RightPoints[index]);
        }

        XAxisX1 = plot.XAxisX1;
        XAxisX2 = plot.XAxisX2;
        XAxisY1 = plot.XAxisY1;
        XAxisY2 = plot.XAxisY2;
        YAxisX1 = plot.YAxisX1;
        YAxisX2 = plot.YAxisX2;
        YAxisY1 = plot.YAxisY1;
        YAxisY2 = plot.YAxisY2;
    }

    private void UpdateAxes(RepresentativeProfilePlotResult plot)
    {

        XAxisX1 = plot.XAxisX1;
        XAxisX2 = plot.XAxisX2;
        XAxisY1 = plot.XAxisY1;
        XAxisY2 = plot.XAxisY2;
        YAxisX1 = plot.YAxisX1;
        YAxisX2 = plot.YAxisX2;
        YAxisY1 = plot.YAxisY1;
        YAxisY2 = plot.YAxisY2;
    }

    private static string BuildMaximumDropStatus(
        string sideText,
        MaximumDropProfileResult? maximumDropProfile)
    {
        if (maximumDropProfile == null)
        {
            return $"{sideText}：未检测到有效掉块";
        }

        return $"{sideText}：最大掉块 {maximumDropProfile.MaximumDropDepth.ToString("F3", CultureInfo.InvariantCulture)} mm，" +
               $"Y={maximumDropProfile.ProfileY.ToString("F3", CultureInfo.InvariantCulture)} mm";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
