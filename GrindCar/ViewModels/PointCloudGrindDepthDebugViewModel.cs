using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;

namespace GrindCar.ViewModels;

/// <summary>
/// Left/Right 原始点云算法验证窗口 ViewModel。
/// </summary>
public class PointCloudGrindDepthDebugViewModel : INotifyPropertyChanged
{
    private const string MissingFileText = "未选择文件";

    private readonly PointCloudGrindDepthDebugWorkflowService _workflowService;
    private readonly ObservableCollection<CombinedGrindDepthResult> _results = new();

    private List<RailProfilePoint> _latestRepresentativePoints = new();
    private List<RailProfilePoint> _latestMaximumDropProfilePoints = new();
    private IReadOnlyList<IReadOnlyList<RailProfilePoint>> _latestMaximumDropProfileSegments =
        Array.Empty<IReadOnlyList<RailProfilePoint>>();
    private MaximumDropProfileResult? _latestLeftMaximumDropProfile;
    private MaximumDropProfileResult? _latestRightMaximumDropProfile;
    private string _leftFilePath = MissingFileText;
    private string _rightFilePath = MissingFileText;
    private string _anglesInput = "-20,-15,-10,-5,-2,0,2,5,10,15,25,35,45,55,65,75,83";
    private string _leftSummaryText = "-";
    private string _rightSummaryText = "-";
    private string _resultCountText = "0";
    private string _statusMessage = "请选择 Left 和 Right 原始点云 CSV 文件。";
    private bool _isBusy;

    public PointCloudGrindDepthDebugViewModel()
        : this(new PointCloudGrindDepthDebugWorkflowService())
    {
    }

    internal PointCloudGrindDepthDebugViewModel(PointCloudGrindDepthDebugWorkflowService workflowService)
    {
        _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CombinedGrindDepthResult> Results => _results;

    public IReadOnlyList<RailProfilePoint> LatestRepresentativePoints => _latestRepresentativePoints;

    public IReadOnlyList<RailProfilePoint> LatestMaximumDropProfilePoints =>
        _latestMaximumDropProfilePoints;

    public IReadOnlyList<IReadOnlyList<RailProfilePoint>> LatestMaximumDropProfileSegments =>
        _latestMaximumDropProfileSegments;

    public MaximumDropProfileResult? LatestLeftMaximumDropProfile =>
        _latestLeftMaximumDropProfile;

    public MaximumDropProfileResult? LatestRightMaximumDropProfile =>
        _latestRightMaximumDropProfile;

    public IReadOnlyList<IReadOnlyList<RailProfilePoint>> LatestLeftMaximumDropProfileSegments =>
        _latestLeftMaximumDropProfile?.ProfileSegments ??
        Array.Empty<IReadOnlyList<RailProfilePoint>>();

    public IReadOnlyList<IReadOnlyList<RailProfilePoint>> LatestRightMaximumDropProfileSegments =>
        _latestRightMaximumDropProfile?.ProfileSegments ??
        Array.Empty<IReadOnlyList<RailProfilePoint>>();

    public string LeftFilePath
    {
        get => _leftFilePath;
        private set
        {
            if (_leftFilePath == value)
            {
                return;
            }

            _leftFilePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCalculate));
        }
    }

    public string RightFilePath
    {
        get => _rightFilePath;
        private set
        {
            if (_rightFilePath == value)
            {
                return;
            }

            _rightFilePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCalculate));
        }
    }

    public string AnglesInput
    {
        get => _anglesInput;
        set
        {
            if (_anglesInput == value)
            {
                return;
            }

            _anglesInput = value;
            OnPropertyChanged();
        }
    }

    public string LeftSummaryText
    {
        get => _leftSummaryText;
        private set
        {
            if (_leftSummaryText == value)
            {
                return;
            }

            _leftSummaryText = value;
            OnPropertyChanged();
        }
    }

    public string RightSummaryText
    {
        get => _rightSummaryText;
        private set
        {
            if (_rightSummaryText == value)
            {
                return;
            }

            _rightSummaryText = value;
            OnPropertyChanged();
        }
    }

    public string ResultCountText
    {
        get => _resultCountText;
        private set
        {
            if (_resultCountText == value)
            {
                return;
            }

            _resultCountText = value;
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
            OnPropertyChanged(nameof(CanSelectFiles));
            OnPropertyChanged(nameof(CanCalculate));
            OnPropertyChanged(nameof(CanViewMaximumDropProfile));
        }
    }

    public bool CanSelectFiles => !IsBusy;

    public bool CanCalculate => !IsBusy &&
        !string.IsNullOrWhiteSpace(LeftFilePath) &&
        !string.IsNullOrWhiteSpace(RightFilePath) &&
        LeftFilePath != MissingFileText &&
        RightFilePath != MissingFileText;

    public bool CanViewMaximumDropProfile => !IsBusy &&
        (_latestLeftMaximumDropProfile != null || _latestRightMaximumDropProfile != null);

    public void SetLeftFilePath(string filePath)
    {
        LeftFilePath = filePath;
        StatusMessage = $"已选择 Left 原始点云: {Path.GetFileName(filePath)}";
    }

    public void SetRightFilePath(string filePath)
    {
        RightFilePath = filePath;
        StatusMessage = $"已选择 Right 原始点云: {Path.GetFileName(filePath)}";
    }

    public async Task<IReadOnlyList<RailProfilePoint>> CalculateAsync()
    {
        IReadOnlyList<int> angles = GrindDepthAngleParser.ParseAngles(AnglesInput);

        try
        {
            IsBusy = true;
            StatusMessage = "正在提取 Left/Right 代表廓形并计算打磨深度...";

            PointCloudGrindDepthDebugCalculationOutput output = await Task.Run(() =>
                    _workflowService.Calculate(LeftFilePath, RightFilePath, angles))
                .ConfigureAwait(true);

            _latestRepresentativePoints = new List<RailProfilePoint>(output.RepresentativePoints);
            _latestLeftMaximumDropProfile = output.LeftMaximumDropProfile;
            _latestRightMaximumDropProfile = output.RightMaximumDropProfile;
            _latestMaximumDropProfilePoints = output.GlobalMaximumDropProfile == null
                ? new List<RailProfilePoint>()
                : new List<RailProfilePoint>(output.GlobalMaximumDropProfile.ProfilePoints);
            _latestMaximumDropProfileSegments = output.GlobalMaximumDropProfile?.ProfileSegments ??
                Array.Empty<IReadOnlyList<RailProfilePoint>>();
            _results.Clear();
            for (int index = 0; index < output.Results.Count; index++)
            {
                _results.Add(output.Results[index]);
            }

            LeftSummaryText = BuildSideSummary(
                output.LeftRepresentativeY,
                output.LeftPointCount,
                output.LeftMaximumDropProfile);
            RightSummaryText = BuildSideSummary(
                output.RightRepresentativeY,
                output.RightPointCount,
                output.RightMaximumDropProfile);
            ResultCountText = output.Results.Count.ToString(CultureInfo.InvariantCulture);
            StatusMessage =
                $"计算完成，合并代表点 {_latestRepresentativePoints.Count.ToString(CultureInfo.InvariantCulture)} 个，结果 {output.Results.Count.ToString(CultureInfo.InvariantCulture)} 条。";
            OnPropertyChanged(nameof(LatestRepresentativePoints));
            OnPropertyChanged(nameof(LatestMaximumDropProfilePoints));
            OnPropertyChanged(nameof(LatestMaximumDropProfileSegments));
            OnPropertyChanged(nameof(LatestLeftMaximumDropProfile));
            OnPropertyChanged(nameof(LatestRightMaximumDropProfile));
            OnPropertyChanged(nameof(LatestLeftMaximumDropProfileSegments));
            OnPropertyChanged(nameof(LatestRightMaximumDropProfileSegments));
            OnPropertyChanged(nameof(CanViewMaximumDropProfile));
            return _latestRepresentativePoints;
        }
        catch
        {
            _latestRepresentativePoints = new List<RailProfilePoint>();
            _latestMaximumDropProfilePoints = new List<RailProfilePoint>();
            _latestMaximumDropProfileSegments = Array.Empty<IReadOnlyList<RailProfilePoint>>();
            _latestLeftMaximumDropProfile = null;
            _latestRightMaximumDropProfile = null;
            _results.Clear();
            ResultCountText = "0";
            OnPropertyChanged(nameof(LatestRepresentativePoints));
            OnPropertyChanged(nameof(LatestMaximumDropProfilePoints));
            OnPropertyChanged(nameof(LatestMaximumDropProfileSegments));
            OnPropertyChanged(nameof(LatestLeftMaximumDropProfile));
            OnPropertyChanged(nameof(LatestRightMaximumDropProfile));
            OnPropertyChanged(nameof(LatestLeftMaximumDropProfileSegments));
            OnPropertyChanged(nameof(LatestRightMaximumDropProfileSegments));
            OnPropertyChanged(nameof(CanViewMaximumDropProfile));
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildSideSummary(
        double representativeY,
        int pointCount,
        MaximumDropProfileResult? maximumDropProfile)
    {
        string representativeText =
            $"代表Y={representativeY.ToString("F3", CultureInfo.InvariantCulture)}，点数={pointCount.ToString(CultureInfo.InvariantCulture)}";
        if (maximumDropProfile == null)
        {
            return $"{representativeText}；未检测到有效向下掉块";
        }

        return $"{representativeText}；最大掉块={maximumDropProfile.MaximumDropDepth.ToString("F3", CultureInfo.InvariantCulture)} mm，" +
               $"Y={maximumDropProfile.ProfileY.ToString("F3", CultureInfo.InvariantCulture)}";
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
