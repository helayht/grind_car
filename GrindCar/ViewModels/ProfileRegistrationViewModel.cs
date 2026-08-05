using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Debug;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.ViewModels;

/// <summary>
/// 代表廓形手动配准窗口 ViewModel。
/// </summary>
public sealed class ProfileRegistrationViewModel : INotifyPropertyChanged
{
    private const string MissingFileText = "未选择文件";
    private const double PlotPadding = 36.0;
    private const double PointDiameter = 5.0;
    private const int StandardSampleCount = 240;
    private const double SuccessErrorThreshold = 0.5;
    private const double TranslationMinimum = -200.0;
    private const double TranslationMaximum = 200.0;
    private const double RotationMinimum = -360.0;
    private const double RotationMaximum = 360.0;
    private const double IcpDeltaTolerance = 1e-4;

    private readonly ProfileRegistrationSettingsStore _settingsStore;
    private readonly ProfileRegistrationTransformService _transformService;
    private readonly ProfileRegistrationPlotMapper _plotMapper = new(PlotPadding);
    private readonly PointCloudRepresentativeProfileService _profileService;
    private readonly RobustIcpRegistrationService _icpService = new();

    private List<PointCloudPoint3D> _leftRawPoints = new();
    private List<PointCloudPoint3D> _rightRawPoints = new();
    private List<RailProfilePoint> _leftBasePoints = new();
    private List<RailProfilePoint> _rightBasePoints = new();
    private double _plotWidth;
    private double _plotHeight;
    private string _leftFilePath = MissingFileText;
    private string _rightFilePath = MissingFileText;
    private string _leftSummaryText = "-";
    private string _rightSummaryText = "-";
    private string _feedbackText = "请选择 Left 和 Right 原始点云 CSV 文件。";
    private Brush _feedbackBrush = Brushes.DimGray;
    private Geometry _standardCurveGeometry = Geometry.Empty;
    private Geometry _leftCurveGeometry = Geometry.Empty;
    private Geometry _rightCurveGeometry = Geometry.Empty;
    private double _xAxisX1;
    private double _xAxisX2;
    private double _xAxisY1;
    private double _xAxisY2;
    private double _yAxisX1;
    private double _yAxisX2;
    private double _yAxisY1;
    private double _yAxisY2;
    private double _pendingLeftDx;
    private double _pendingLeftDy;
    private double _pendingLeftRotationDegrees;
    private double _pendingRightDx;
    private double _pendingRightDy;
    private double _pendingRightRotationDegrees;
    private bool _pendingLeftIsMirrored;
    private bool _pendingRightIsMirrored;
    private double? _pendingLeftXMin;
    private double? _pendingLeftXMax;
    private double? _pendingRightXMin;
    private double? _pendingRightXMax;
    private double _appliedLeftDx;
    private double _appliedLeftDy;
    private double _appliedLeftRotationDegrees;
    private double _appliedRightDx;
    private double _appliedRightDy;
    private double _appliedRightRotationDegrees;
    private bool _appliedLeftIsMirrored;
    private bool _appliedRightIsMirrored;
    private double? _appliedLeftXMin;
    private double? _appliedLeftXMax;
    private double? _appliedRightXMin;
    private double? _appliedRightXMax;

    public ProfileRegistrationViewModel()
        : this(new ProfileRegistrationSettingsStore(), new ProfileRegistrationTransformService())
    {
    }

    internal ProfileRegistrationViewModel(
        ProfileRegistrationSettingsStore settingsStore,
        ProfileRegistrationTransformService transformService)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
        _profileService = new PointCloudRepresentativeProfileService(_settingsStore, _transformService);
        LoadExistingSettingsIfAvailable();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RepresentativeProfileScreenPoint> LeftPointItems { get; } = new();

    public ObservableCollection<RepresentativeProfileScreenPoint> RightPointItems { get; } = new();

    public string LeftFilePath
    {
        get => _leftFilePath;
        private set => SetField(ref _leftFilePath, value);
    }

    public string RightFilePath
    {
        get => _rightFilePath;
        private set => SetField(ref _rightFilePath, value);
    }

    public string LeftSummaryText
    {
        get => _leftSummaryText;
        private set => SetField(ref _leftSummaryText, value);
    }

    public string RightSummaryText
    {
        get => _rightSummaryText;
        private set => SetField(ref _rightSummaryText, value);
    }

    public string FeedbackText
    {
        get => _feedbackText;
        private set => SetField(ref _feedbackText, value);
    }

    public Brush FeedbackBrush
    {
        get => _feedbackBrush;
        private set => SetField(ref _feedbackBrush, value);
    }

    public Geometry StandardCurveGeometry
    {
        get => _standardCurveGeometry;
        private set => SetField(ref _standardCurveGeometry, value);
    }

    public Geometry LeftCurveGeometry
    {
        get => _leftCurveGeometry;
        private set => SetField(ref _leftCurveGeometry, value);
    }

    public Geometry RightCurveGeometry
    {
        get => _rightCurveGeometry;
        private set => SetField(ref _rightCurveGeometry, value);
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

    public double PendingLeftDx
    {
        get => _pendingLeftDx;
        set => SetPendingField(ref _pendingLeftDx, ClampTranslation(value));
    }

    public double PendingLeftDy
    {
        get => _pendingLeftDy;
        set => SetPendingField(ref _pendingLeftDy, ClampTranslation(value));
    }

    public double PendingLeftRotationDegrees
    {
        get => _pendingLeftRotationDegrees;
        set => SetPendingField(ref _pendingLeftRotationDegrees, ClampRotation(value));
    }

    public double PendingRightDx
    {
        get => _pendingRightDx;
        set => SetPendingField(ref _pendingRightDx, ClampTranslation(value));
    }

    public double PendingRightDy
    {
        get => _pendingRightDy;
        set => SetPendingField(ref _pendingRightDy, ClampTranslation(value));
    }

    public double PendingRightRotationDegrees
    {
        get => _pendingRightRotationDegrees;
        set => SetPendingField(ref _pendingRightRotationDegrees, ClampRotation(value));
    }

    public bool PendingLeftIsMirrored
    {
        get => _pendingLeftIsMirrored;
        set => SetPendingField(ref _pendingLeftIsMirrored, value);
    }

    public bool PendingRightIsMirrored
    {
        get => _pendingRightIsMirrored;
        set => SetPendingField(ref _pendingRightIsMirrored, value);
    }

    public double? PendingLeftXMin
    {
        get => _pendingLeftXMin;
        set
        {
            if (_pendingLeftXMin == value)
            {
                return;
            }

            _pendingLeftXMin = value;
            OnPropertyChanged();
        }
    }

    public double? PendingLeftXMax
    {
        get => _pendingLeftXMax;
        set
        {
            if (_pendingLeftXMax == value)
            {
                return;
            }

            _pendingLeftXMax = value;
            OnPropertyChanged();
        }
    }

    public double? PendingRightXMin
    {
        get => _pendingRightXMin;
        set
        {
            if (_pendingRightXMin == value)
            {
                return;
            }

            _pendingRightXMin = value;
            OnPropertyChanged();
        }
    }

    public double? PendingRightXMax
    {
        get => _pendingRightXMax;
        set
        {
            if (_pendingRightXMax == value)
            {
                return;
            }

            _pendingRightXMax = value;
            OnPropertyChanged();
        }
    }

    public double AppliedLeftDx => _appliedLeftDx;

    public double AppliedLeftDy => _appliedLeftDy;

    public double AppliedLeftRotationDegrees => _appliedLeftRotationDegrees;

    public double AppliedRightDx => _appliedRightDx;

    public double AppliedRightDy => _appliedRightDy;

    public double AppliedRightRotationDegrees => _appliedRightRotationDegrees;

    public bool AppliedLeftIsMirrored => _appliedLeftIsMirrored;

    public bool AppliedRightIsMirrored => _appliedRightIsMirrored;

    public void SetLeftFilePath(string filePath)
    {
        List<PointCloudPoint3D> rawPoints = PointCloudCsvReader.ReadPointsFromCsv(filePath);
        List<RailProfilePoint> profilePoints = BuildProfilePoints(
            rawPoints,
            BuildAppliedLeftParameters(),
            PointCloudDeviceSide.Left);
        _leftRawPoints = rawPoints;
        _leftBasePoints = profilePoints;
        LeftFilePath = filePath;
        LeftSummaryText = BuildSummaryText(_leftBasePoints);
        RefreshPlot();
    }

    public void SetRightFilePath(string filePath)
    {
        List<PointCloudPoint3D> rawPoints = PointCloudCsvReader.ReadPointsFromCsv(filePath);
        List<RailProfilePoint> profilePoints = BuildProfilePoints(
            rawPoints,
            BuildAppliedRightParameters(),
            PointCloudDeviceSide.Right);
        _rightRawPoints = rawPoints;
        _rightBasePoints = profilePoints;
        RightFilePath = filePath;
        RightSummaryText = BuildSummaryText(_rightBasePoints);
        RefreshPlot();
    }

    public void ResetParameters()
    {
        SetPendingParameters(
            new ProfileRegistrationParameters(0.0, 0.0, 0.0),
            new ProfileRegistrationParameters(0.0, 0.0, 0.0));
        SetAppliedParameters(
            new ProfileRegistrationParameters(0.0, 0.0, 0.0),
            new ProfileRegistrationParameters(0.0, 0.0, 0.0));
        _pendingLeftXMin = null;
        _pendingLeftXMax = null;
        _pendingRightXMin = null;
        _pendingRightXMax = null;
        OnPropertyChanged(nameof(PendingLeftXMin));
        OnPropertyChanged(nameof(PendingLeftXMax));
        OnPropertyChanged(nameof(PendingRightXMin));
        OnPropertyChanged(nameof(PendingRightXMax));
        RebuildCachedProfiles();
        RefreshPlot();
    }

    public void ApplyPendingParameters()
    {
        SetAppliedParameters(BuildPendingLeftParameters(), BuildPendingRightParameters());
        RebuildCachedProfiles();
        RefreshPlot();
    }

    public void SaveSettings()
    {
        var settings = new ProfileRegistrationSettings
        {
            Left = BuildAppliedLeftParameters(),
            Right = BuildAppliedRightParameters()
        };
        _settingsStore.Save(settings);
        FeedbackText = $"配准参数已保存：{_settingsStore.ResolveConfigurationFilePath()}";
        FeedbackBrush = Brushes.ForestGreen;
    }

    public void AutoAlignLeft()
    {
        if (_leftRawPoints.Count == 0 || _leftBasePoints.Count == 0) return;

        ProfileRegistrationParameters? composedParameters = CalculateSinglePassAutoAlignment(
            _leftRawPoints,
            _leftBasePoints,
            BuildAppliedLeftParameters(),
            PointCloudDeviceSide.Left);
        if (composedParameters == null)
        {
            return;
        }

        PendingLeftDx = ClampTranslation(composedParameters.Dx);
        PendingLeftDy = ClampTranslation(composedParameters.Dy);
        PendingLeftRotationDegrees = ClampRotation(composedParameters.RotationDegrees);
        PendingLeftIsMirrored = composedParameters.IsMirrored;
        PendingLeftXMin = composedParameters.XMin;
        PendingLeftXMax = composedParameters.XMax;

        SetAppliedParameters(composedParameters, BuildAppliedRightParameters());
        RebuildCachedProfile(PointCloudDeviceSide.Left);
        RefreshPlot();
    }

    public void AutoAlignRight()
    {
        if (_rightRawPoints.Count == 0 || _rightBasePoints.Count == 0) return;

        ProfileRegistrationParameters? composedParameters = CalculateSinglePassAutoAlignment(
            _rightRawPoints,
            _rightBasePoints,
            BuildAppliedRightParameters(),
            PointCloudDeviceSide.Right);
        if (composedParameters == null)
        {
            return;
        }

        PendingRightDx = ClampTranslation(composedParameters.Dx);
        PendingRightDy = ClampTranslation(composedParameters.Dy);
        PendingRightRotationDegrees = ClampRotation(composedParameters.RotationDegrees);
        PendingRightIsMirrored = composedParameters.IsMirrored;
        PendingRightXMin = composedParameters.XMin;
        PendingRightXMax = composedParameters.XMax;

        SetAppliedParameters(BuildAppliedLeftParameters(), composedParameters);
        RebuildCachedProfile(PointCloudDeviceSide.Right);
        RefreshPlot();
    }

    public void UpdatePlot(double plotWidth, double plotHeight)
    {
        _plotWidth = plotWidth;
        _plotHeight = plotHeight;
        RefreshPlot();
    }

    private void LoadExistingSettingsIfAvailable()
    {
        try
        {
            ProfileRegistrationSettings? settings = _settingsStore.Load();
            if (settings == null)
            {
                return;
            }

            SetPendingParameters(settings.Left!, settings.Right!);
            SetAppliedParameters(settings.Left!, settings.Right!);
            FeedbackText = "已加载现有代表廓形配准参数。";
        }
        catch (Exception ex)
        {
            FeedbackText = $"现有配准参数无效，请重新配准并保存：{ex.Message}";
            FeedbackBrush = Brushes.DarkOrange;
        }
    }

    private List<RailProfilePoint> BuildProfilePoints(
        IReadOnlyList<PointCloudPoint3D> rawPoints,
        ProfileRegistrationParameters parameters,
        PointCloudDeviceSide side)
    {
        if (rawPoints.Count == 0)
        {
            return new List<RailProfilePoint>();
        }

        MedianSectionExtractionResult result =
            _profileService.ExtractMedianSectionProfileFromPoints(rawPoints, side, parameters);
        return result.ProfilePoints.ToList();
    }

    private void RebuildCachedProfiles()
    {
        RebuildCachedProfile(PointCloudDeviceSide.Left);
        RebuildCachedProfile(PointCloudDeviceSide.Right);
    }

    private void RebuildCachedProfile(PointCloudDeviceSide side)
    {
        if (side == PointCloudDeviceSide.Left)
        {
            _leftBasePoints = BuildProfilePoints(
                _leftRawPoints,
                BuildAppliedLeftParameters(),
                PointCloudDeviceSide.Left);
            LeftSummaryText = BuildSummaryText(_leftBasePoints);
            return;
        }

        _rightBasePoints = BuildProfilePoints(
            _rightRawPoints,
            BuildAppliedRightParameters(),
            PointCloudDeviceSide.Right);
        RightSummaryText = BuildSummaryText(_rightBasePoints);
    }

    private ProfileRegistrationParameters? CalculateSinglePassAutoAlignment(
        IReadOnlyList<PointCloudPoint3D> rawPoints,
        IReadOnlyList<RailProfilePoint> currentProfile,
        ProfileRegistrationParameters currentParameters,
        PointCloudDeviceSide side)
    {
        IReadOnlyList<RailProfilePoint> standardPoints = BuildStandardPoints();
        ProfileRegistrationParameters icpDelta =
            _icpService.Align(currentProfile, standardPoints);
        if (IsNegligibleIcpDelta(icpDelta))
        {
            return null;
        }

        return ClampParameters(
            _transformService.ComposeRawFirstWithGlobalDelta(
                rawPoints,
                currentParameters,
                icpDelta,
                side));
    }

    private static bool IsNegligibleIcpDelta(ProfileRegistrationParameters delta)
    {
        return Math.Abs(delta.Dx) <= IcpDeltaTolerance &&
               Math.Abs(delta.Dy) <= IcpDeltaTolerance &&
               Math.Abs(delta.RotationDegrees) <= IcpDeltaTolerance;
    }

    private static ProfileRegistrationParameters ClampParameters(ProfileRegistrationParameters parameters)
    {
        return new ProfileRegistrationParameters(
            ClampTranslation(parameters.Dx),
            ClampTranslation(parameters.Dy),
            ClampRotation(parameters.RotationDegrees),
            parameters.IsMirrored)
        {
            XMin = parameters.XMin,
            XMax = parameters.XMax
        };
    }

    private void RefreshPlot()
    {
        if (_plotWidth <= PlotPadding * 2.0 || _plotHeight <= PlotPadding * 2.0)
        {
            return;
        }

        IReadOnlyList<RailProfilePoint> leftPoints = _leftBasePoints;
        IReadOnlyList<RailProfilePoint> rightPoints = _rightBasePoints;
        IReadOnlyList<RailProfilePoint> standardPoints = BuildStandardPoints();
        IReadOnlyList<RailProfilePoint> allPoints = leftPoints
            .Concat(rightPoints)
            .Concat(standardPoints)
            .ToArray();

        if (allPoints.Count == 0)
        {
            UpdateFeedback(leftPoints, rightPoints, standardPoints);
            return;
        }

        _plotMapper.Update(allPoints, _plotWidth, _plotHeight);
        IReadOnlyList<Point> standardScreenPoints = _plotMapper.MapToScreen(standardPoints);
        IReadOnlyList<Point> leftScreenPoints = _plotMapper.MapToScreen(leftPoints);
        IReadOnlyList<Point> rightScreenPoints = _plotMapper.MapToScreen(rightPoints);

        StandardCurveGeometry = BuildPolylineGeometry(standardScreenPoints);
        LeftCurveGeometry = BuildPolylineGeometry(leftScreenPoints);
        RightCurveGeometry = BuildPolylineGeometry(rightScreenPoints);
        ReplacePointItems(LeftPointItems, leftScreenPoints);
        ReplacePointItems(RightPointItems, rightScreenPoints);

        XAxisX1 = PlotPadding;
        XAxisX2 = _plotWidth - PlotPadding;
        XAxisY1 = _plotMapper.MapY(0.0);
        XAxisY2 = XAxisY1;
        YAxisX1 = _plotMapper.MapX(0.0);
        YAxisX2 = YAxisX1;
        YAxisY1 = PlotPadding;
        YAxisY2 = _plotHeight - PlotPadding;
        UpdateFeedback(leftPoints, rightPoints, standardPoints);
    }

    private void UpdateFeedback(
        IReadOnlyList<RailProfilePoint> leftPoints,
        IReadOnlyList<RailProfilePoint> rightPoints,
        IReadOnlyList<RailProfilePoint> standardPoints)
    {
        if (leftPoints.Count == 0 || rightPoints.Count == 0)
        {
            FeedbackText = "请选择 Left 和 Right 原始点云 CSV 文件。";
            FeedbackBrush = Brushes.DimGray;
            return;
        }

        ProfileRegistrationErrorMetrics leftMetrics =
            _transformService.CalculateStandardErrorMetrics(leftPoints, standardPoints);
        ProfileRegistrationErrorMetrics rightMetrics =
            _transformService.CalculateStandardErrorMetrics(rightPoints, standardPoints);
        string leftText = FormatMetrics(leftMetrics);
        string rightText = FormatMetrics(rightMetrics);
        if (!double.IsNaN(leftMetrics.InlierRootMeanSquareDistance) &&
            !double.IsNaN(rightMetrics.InlierRootMeanSquareDistance) &&
            leftMetrics.InlierRootMeanSquareDistance <= SuccessErrorThreshold &&
            rightMetrics.InlierRootMeanSquareDistance <= SuccessErrorThreshold)
        {
            FeedbackText = $"配准成功，轨面已闭合。Left {leftText}；Right {rightText}";
            FeedbackBrush = Brushes.ForestGreen;
            return;
        }

        FeedbackText = $"继续调整配准参数。Left {leftText}；Right {rightText}";
        FeedbackBrush = Brushes.DarkOrange;
    }

    private static string BuildSummaryText(IReadOnlyCollection<RailProfilePoint> points)
    {
        if (points.Count == 0)
        {
            return "点数=0";
        }

        return $"点数={points.Count.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatError(double error)
    {
        return double.IsNaN(error)
            ? "--"
            : error.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private static string FormatMetrics(ProfileRegistrationErrorMetrics metrics)
    {
        return $"内点RMSE={FormatError(metrics.InlierRootMeanSquareDistance)} mm，" +
               $"全点均值={FormatError(metrics.AverageDistance)} mm，" +
               $"P95={FormatError(metrics.Percentile95Distance)} mm";
    }

    private ProfileRegistrationParameters BuildPendingLeftParameters()
    {
        return new ProfileRegistrationParameters(
            PendingLeftDx,
            PendingLeftDy,
            PendingLeftRotationDegrees,
            PendingLeftIsMirrored)
        {
            XMin = PendingLeftXMin,
            XMax = PendingLeftXMax
        };
    }

    private ProfileRegistrationParameters BuildPendingRightParameters()
    {
        return new ProfileRegistrationParameters(
            PendingRightDx,
            PendingRightDy,
            PendingRightRotationDegrees,
            PendingRightIsMirrored)
        {
            XMin = PendingRightXMin,
            XMax = PendingRightXMax
        };
    }

    private ProfileRegistrationParameters BuildAppliedLeftParameters()
    {
        return new ProfileRegistrationParameters(
            AppliedLeftDx,
            AppliedLeftDy,
            AppliedLeftRotationDegrees,
            AppliedLeftIsMirrored)
        {
            XMin = _appliedLeftXMin,
            XMax = _appliedLeftXMax
        };
    }

    private ProfileRegistrationParameters BuildAppliedRightParameters()
    {
        return new ProfileRegistrationParameters(
            AppliedRightDx,
            AppliedRightDy,
            AppliedRightRotationDegrees,
            AppliedRightIsMirrored)
        {
            XMin = _appliedRightXMin,
            XMax = _appliedRightXMax
        };
    }

    private void SetPendingParameters(
        ProfileRegistrationParameters leftParameters,
        ProfileRegistrationParameters rightParameters)
    {
        _pendingLeftDx = ClampTranslation(leftParameters.Dx);
        _pendingLeftDy = ClampTranslation(leftParameters.Dy);
        _pendingLeftRotationDegrees = ClampRotation(leftParameters.RotationDegrees);
        _pendingLeftIsMirrored = leftParameters.IsMirrored;
        _pendingLeftXMin = leftParameters.XMin;
        _pendingLeftXMax = leftParameters.XMax;
        _pendingRightDx = ClampTranslation(rightParameters.Dx);
        _pendingRightDy = ClampTranslation(rightParameters.Dy);
        _pendingRightRotationDegrees = ClampRotation(rightParameters.RotationDegrees);
        _pendingRightIsMirrored = rightParameters.IsMirrored;
        _pendingRightXMin = rightParameters.XMin;
        _pendingRightXMax = rightParameters.XMax;
        NotifyPendingPropertiesChanged();
    }

    private void SetAppliedParameters(
        ProfileRegistrationParameters leftParameters,
        ProfileRegistrationParameters rightParameters)
    {
        _appliedLeftDx = ClampTranslation(leftParameters.Dx);
        _appliedLeftDy = ClampTranslation(leftParameters.Dy);
        _appliedLeftRotationDegrees = ClampRotation(leftParameters.RotationDegrees);
        _appliedLeftIsMirrored = leftParameters.IsMirrored;
        _appliedLeftXMin = leftParameters.XMin;
        _appliedLeftXMax = leftParameters.XMax;
        _appliedRightDx = ClampTranslation(rightParameters.Dx);
        _appliedRightDy = ClampTranslation(rightParameters.Dy);
        _appliedRightRotationDegrees = ClampRotation(rightParameters.RotationDegrees);
        _appliedRightIsMirrored = rightParameters.IsMirrored;
        _appliedRightXMin = rightParameters.XMin;
        _appliedRightXMax = rightParameters.XMax;
        NotifyAppliedPropertiesChanged();
    }

    private void NotifyPendingPropertiesChanged()
    {
        OnPropertyChanged(nameof(PendingLeftDx));
        OnPropertyChanged(nameof(PendingLeftDy));
        OnPropertyChanged(nameof(PendingLeftRotationDegrees));
        OnPropertyChanged(nameof(PendingLeftIsMirrored));
        OnPropertyChanged(nameof(PendingRightDx));
        OnPropertyChanged(nameof(PendingRightDy));
        OnPropertyChanged(nameof(PendingRightRotationDegrees));
        OnPropertyChanged(nameof(PendingRightIsMirrored));
        OnPropertyChanged(nameof(PendingLeftXMin));
        OnPropertyChanged(nameof(PendingLeftXMax));
        OnPropertyChanged(nameof(PendingRightXMin));
        OnPropertyChanged(nameof(PendingRightXMax));
    }

    private void NotifyAppliedPropertiesChanged()
    {
        OnPropertyChanged(nameof(AppliedLeftDx));
        OnPropertyChanged(nameof(AppliedLeftDy));
        OnPropertyChanged(nameof(AppliedLeftRotationDegrees));
        OnPropertyChanged(nameof(AppliedLeftIsMirrored));
        OnPropertyChanged(nameof(AppliedRightDx));
        OnPropertyChanged(nameof(AppliedRightDy));
        OnPropertyChanged(nameof(AppliedRightRotationDegrees));
        OnPropertyChanged(nameof(AppliedRightIsMirrored));
    }

    private static IReadOnlyList<RailProfilePoint> BuildStandardPoints()
    {
        var points = new List<RailProfilePoint>(StandardSampleCount + 1);
        for (int index = 0; index <= StandardSampleCount; index++)
        {
            double x = StandardRailProfileSolver.LeftBoundaryX +
                (StandardRailProfileSolver.RightBoundaryX - StandardRailProfileSolver.LeftBoundaryX) *
                index / StandardSampleCount;
            double y = StandardRailProfileSolver.RailSurfaceFun(x);
            if (!double.IsNaN(y) && !double.IsInfinity(y))
            {
                points.Add(new RailProfilePoint(x, y));
            }
        }

        return points;
    }

    private static void ReplacePointItems(
        ObservableCollection<RepresentativeProfileScreenPoint> target,
        IReadOnlyList<Point> points)
    {
        target.Clear();
        for (int index = 0; index < points.Count; index++)
        {
            target.Add(new RepresentativeProfileScreenPoint(
                points[index].X - PointDiameter / 2.0,
                points[index].Y - PointDiameter / 2.0));
        }
    }

    private static Geometry BuildPolylineGeometry(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(points[0], false, false);
        for (int index = 1; index < points.Count; index++)
        {
            context.LineTo(points[index], true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static double ClampTranslation(double value)
    {
        return Math.Clamp(value, TranslationMinimum, TranslationMaximum);
    }

    private static double ClampRotation(double value)
    {
        return Math.Clamp(value, RotationMinimum, RotationMaximum);
    }

    private void SetPendingField(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(field - value) < 1e-9)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void SetPendingField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
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
