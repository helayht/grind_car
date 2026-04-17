using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private readonly RepresentativeProfileComparisonSnapshot _snapshot;

    private Geometry _representativeCurveGeometry = Geometry.Empty;
    private Geometry _standardCurveGeometry = Geometry.Empty;
    private double _xAxisX1;
    private double _xAxisX2;
    private double _xAxisY1;
    private double _xAxisY2;
    private double _yAxisX1;
    private double _yAxisX2;
    private double _yAxisY1;
    private double _yAxisY2;

    public RepresentativeProfileComparisonViewModel(IReadOnlyList<RailProfilePoint> representativePoints)
    {
        _snapshot = _comparisonService.BuildSnapshot(representativePoints);
        PointCountText = _snapshot.PointCountText;
        XRangeText = _snapshot.XRangeText;
        YRangeText = _snapshot.YRangeText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RepresentativeProfileScreenPoint> RepresentativePointItems { get; } = new();

    public string PointCountText { get; }

    public string XRangeText { get; }

    public string YRangeText { get; }

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
        RepresentativeProfilePlotResult plot = _comparisonService.BuildPlot(_snapshot, plotWidth, plotHeight);

        RepresentativeCurveGeometry = plot.RepresentativeCurveGeometry;
        StandardCurveGeometry = plot.StandardCurveGeometry;

        RepresentativePointItems.Clear();
        for (int index = 0; index < plot.RepresentativePoints.Count; index++)
        {
            RepresentativePointItems.Add(plot.RepresentativePoints[index]);
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
