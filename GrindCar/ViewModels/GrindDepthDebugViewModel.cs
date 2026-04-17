using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;

namespace GrindCar.ViewModels;

/// <summary>
/// 打磨深度调试窗口 ViewModel。
/// </summary>
public class GrindDepthDebugViewModel : INotifyPropertyChanged
{
    private readonly GrindDepthDebugWorkflowService _workflowService = new();
    private readonly ObservableCollection<GrindDepthResult> _requiredResults = new();
    private readonly ObservableCollection<DetectedGrindDepthResult> _detectedResults = new();

    private List<RailProfilePoint> _latestRepresentativePoints = new();
    private string _anglesInput = "-20,-15,-10,-5,-2,0,2,5,10,15,25,35,45,55,65,75,83";
    private string _resultCountText = "0";
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GrindDepthResult> RequiredResults => _requiredResults;

    public ObservableCollection<DetectedGrindDepthResult> DetectedResults => _detectedResults;

    public IReadOnlyList<RailProfilePoint> LatestRepresentativePoints => _latestRepresentativePoints;

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
            OnPropertyChanged(nameof(CanEditAngles));
            OnPropertyChanged(nameof(CanRunActions));
            OnPropertyChanged(nameof(CanExportRepresentativePoints));
        }
    }

    public bool CanEditAngles => !IsBusy;

    public bool CanRunActions => !IsBusy;

    public bool CanExportRepresentativePoints => !IsBusy && _latestRepresentativePoints.Count > 0;

    public async Task<IReadOnlyList<RailProfilePoint>> CalculateAsync()
    {
        IReadOnlyList<int> angles = GrindDepthAngleParser.ParseAngles(AnglesInput);

        try
        {
            IsBusy = true;
            StatusMessage = $"正在计算 {angles.Count.ToString(CultureInfo.InvariantCulture)} 个角度的需要打磨深度，并保存检测基线...";

            GrindDepthDebugCalculationOutput output =
                await Task.Run(() => _workflowService.CalculateAndSaveBaseline(angles)).ConfigureAwait(true);

            _latestRepresentativePoints = new List<RailProfilePoint>(output.RepresentativePoints);

            _requiredResults.Clear();
            for (int index = 0; index < output.Results.Count; index++)
            {
                _requiredResults.Add(output.Results[index]);
            }

            ResultCountText = output.Results.Count.ToString(CultureInfo.InvariantCulture);
            StatusMessage = $"计算完成，共得到 {output.Results.Count.ToString(CultureInfo.InvariantCulture)} 条需要打磨深度结果，检测基线已更新。";
            OnPropertyChanged(nameof(LatestRepresentativePoints));
            OnPropertyChanged(nameof(CanExportRepresentativePoints));
            return _latestRepresentativePoints;
        }
        catch
        {
            _latestRepresentativePoints = new List<RailProfilePoint>();
            _requiredResults.Clear();
            ResultCountText = "0";
            OnPropertyChanged(nameof(LatestRepresentativePoints));
            OnPropertyChanged(nameof(CanExportRepresentativePoints));
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DetectAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "正在加载检测基线并检测已打磨深度...";

            GrindDepthDebugDetectionOutput output =
                await Task.Run(() => _workflowService.DetectFromLatestBaseline()).ConfigureAwait(true);

            _detectedResults.Clear();
            for (int index = 0; index < output.Results.Count; index++)
            {
                _detectedResults.Add(output.Results[index]);
            }

            ResultCountText = output.Results.Count.ToString(CultureInfo.InvariantCulture);
            StatusMessage =
                $"检测完成，基线时间 {output.BaselineCreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)}，共检测 {output.Results.Count.ToString(CultureInfo.InvariantCulture)} 个角度。";
        }
        catch
        {
            _detectedResults.Clear();
            ResultCountText = "0";
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ExportRepresentativePoints(string outputPath)
    {
        RepresentativePointsCsvExporter.Export(outputPath, _latestRepresentativePoints);
        StatusMessage = $"代表点导出完成: {outputPath}";
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