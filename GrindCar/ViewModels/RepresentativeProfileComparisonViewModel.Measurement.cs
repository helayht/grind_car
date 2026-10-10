using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GrindCar.Services.Measurement;
using GrindCar.Services.Rail.Debug;

namespace GrindCar.ViewModels;

public sealed partial class RepresentativeProfileComparisonViewModel
{
    private readonly Dictionary<int, RepresentativeProfileComparisonSnapshot> _measurementSnapshots = new();
    private readonly Dictionary<int, string> _measurementErrors = new();
    private RepresentativeProfileBounds? _commonBounds;
    private MeasurementRepresentativeProfile? _selectedProfile;
    private double _plotWidth;
    private double _plotHeight;
    private string _plotError = string.Empty;

    /// <summary>保留全部组的序号；无效组不阻断其他组或后续深度确认。</summary>
    public RepresentativeProfileComparisonViewModel(IReadOnlyList<MeasurementRepresentativeProfile> profiles)
    {
        IsMeasurementComparison = true;
        Profiles = Array.AsReadOnly(profiles.ToArray());
        PointCountLabelText = PointCountText = XRangeText = YRangeText = string.Empty;
        HeaderText = "测量代表廓形对比";
        CurveDescriptionText = "代表廓形与标准廓形";
        LeftStatusText = RightStatusText = string.Empty;
        MaximumDropLegendVisibility = Visibility.Collapsed;
        foreach (MeasurementRepresentativeProfile profile in Profiles)
        {
            try
            {
                if (profile.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                    throw new InvalidOperationException("代表点含非有限坐标。");
                _measurementSnapshots.Add(profile.SampleIndex, _comparisonService.BuildSnapshot(profile.Points));
            }
            catch (Exception ex)
            {
                _measurementErrors[profile.SampleIndex] = ex.Message;
            }
        }
        if (_measurementSnapshots.Count > 0)
        {
            RepresentativeProfileBounds[] bounds = _measurementSnapshots.Values.Select(snapshot => snapshot.Bounds).ToArray();
            _commonBounds = new RepresentativeProfileBounds(bounds.Min(b => b.MinX), bounds.Max(b => b.MaxX),
                bounds.Min(b => b.MinY), bounds.Max(b => b.MaxY));
        }
        _selectedProfile = Profiles.FirstOrDefault();
        UpdateMeasurementPlot(0, 0);
    }

    public bool IsMeasurementComparison { get; }
    public bool HasDrawableProfiles => _measurementSnapshots.Count > 0;
    public IReadOnlyList<MeasurementRepresentativeProfile> Profiles { get; } = Array.Empty<MeasurementRepresentativeProfile>();
    public Visibility MeasurementControlsVisibility => IsMeasurementComparison ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProfileSelectorVisibility => Profiles.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    public string PlotError { get => _plotError; private set => SetField(ref _plotError, value); }
    public MeasurementRepresentativeProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (value == _selectedProfile) return;
            SetField(ref _selectedProfile, value);
            UpdateMeasurementPlot(_plotWidth, _plotHeight);
        }
    }

    private void UpdateMeasurementPlot(double width, double height)
    {
        _plotWidth = width;
        _plotHeight = height;
        RepresentativeCurveGeometry = StandardCurveGeometry = LeftCurveGeometry = RightCurveGeometry = Geometry.Empty;
        RepresentativePointItems.Clear();
        LeftPointItems.Clear();
        RightPointItems.Clear();
        MaximumDropMarkerItems.Clear();
        UpdateAxes(RepresentativeProfilePlotResult.Empty);
        PlotError = string.Empty;
        if (_selectedProfile == null)
        {
            PlotError = "本次测量没有可显示的代表廓形。关闭窗口后可继续确认打磨深度。";
            return;
        }
        if (!_measurementSnapshots.TryGetValue(_selectedProfile.SampleIndex, out RepresentativeProfileComparisonSnapshot? snapshot))
        {
            PlotError = $"{_selectedProfile.DisplayName}无法绘图：{_measurementErrors[_selectedProfile.SampleIndex]}";
            return;
        }
        try
        {
            RepresentativeProfilePlotResult plot = _comparisonService.BuildPlot(snapshot, width, height,
                RepresentativeProfileCurveStyle.Smooth, _commonBounds);
            RepresentativeCurveGeometry = plot.RepresentativeCurveGeometry;
            StandardCurveGeometry = plot.StandardCurveGeometry;
            UpdateAxes(plot);
        }
        catch (Exception ex)
        {
            PlotError = $"{_selectedProfile.DisplayName}无法绘图：{ex.Message}";
        }
    }
}
