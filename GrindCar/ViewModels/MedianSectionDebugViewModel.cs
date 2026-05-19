using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Debug;

namespace GrindCar.ViewModels;

/// <summary>
/// 代表截面调试窗口 ViewModel。
/// </summary>
public class MedianSectionDebugViewModel : INotifyPropertyChanged
{
    private readonly MedianSectionDebugWorkflowService _workflowService = new();
    private readonly ObservableCollection<MedianSectionPointDisplayItem> _pointRows = new();

    private List<RailProfilePoint> _currentPoints = new();
    private double? _currentRepresentativeY;
    private string _importedFilePath = "未导入文件";
    private string _representativeYText = "-";
    private string _representativePointCountText = "0";
    private string _xRangeText = "-";
    private string _zRangeText = "-";
    private string _statusText = "请选择一个 .csv 点云文件。";
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MedianSectionPointDisplayItem> PointRows => _pointRows;

    public string ImportedFilePath
    {
        get => _importedFilePath;
        private set
        {
            if (_importedFilePath == value)
            {
                return;
            }

            _importedFilePath = value;
            OnPropertyChanged();
        }
    }

    public string RepresentativeYText
    {
        get => _representativeYText;
        private set
        {
            if (_representativeYText == value)
            {
                return;
            }

            _representativeYText = value;
            OnPropertyChanged();
        }
    }

    public string RepresentativePointCountText
    {
        get => _representativePointCountText;
        private set
        {
            if (_representativePointCountText == value)
            {
                return;
            }

            _representativePointCountText = value;
            OnPropertyChanged();
        }
    }

    public string XRangeText
    {
        get => _xRangeText;
        private set
        {
            if (_xRangeText == value)
            {
                return;
            }

            _xRangeText = value;
            OnPropertyChanged();
        }
    }

    public string ZRangeText
    {
        get => _zRangeText;
        private set
        {
            if (_zRangeText == value)
            {
                return;
            }

            _zRangeText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
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
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(CanExport));
        }
    }

    public bool CanImport => !IsBusy;

    public bool CanExport => !IsBusy && _currentPoints.Count > 0 && _currentRepresentativeY.HasValue;

    public async Task ImportAsync(string filePath)
    {
        ImportedFilePath = filePath;
        StatusText = $"正在提取平均代表截面: {filePath}";

        try
        {
            IsBusy = true;
            MedianSectionExtractionResult extractionResult =
                await Task.Run(() => _workflowService.Extract(filePath)).ConfigureAwait(true);

            ApplyProfilePoints(extractionResult);
            StatusText = $"提取完成，共得到 {_currentPoints.Count.ToString(CultureInfo.InvariantCulture)} 个代表点。";
        }
        catch
        {
            ClearCurrentPoints();
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Export(string outputPath)
    {
        if (_currentRepresentativeY == null)
        {
            throw new InvalidOperationException("当前未加载代表截面数据。");
        }

        _workflowService.Export(outputPath, _currentRepresentativeY.Value, _currentPoints);
        StatusText = $"代表截面坐标已导出: {outputPath}";
    }

    public string BuildDefaultExportFileName()
    {
        string sourceFileName = string.IsNullOrWhiteSpace(ImportedFilePath) || ImportedFilePath == "未导入文件"
            ? "representative_section"
            : Path.GetFileNameWithoutExtension(ImportedFilePath);

        string representativeYText = _currentRepresentativeY?.ToString("F6", CultureInfo.InvariantCulture) ?? "unknown";
        string safeRepresentativeYText = representativeYText.Replace('.', '_');
        return $"{sourceFileName}_representative_section_{safeRepresentativeYText}.csv";
    }

    public void SetErrorStatus(string status)
    {
        StatusText = status;
    }

    private void ApplyProfilePoints(MedianSectionExtractionResult extractionResult)
    {
        _currentRepresentativeY = extractionResult.RepresentativeY;
        RepresentativeYText = extractionResult.RepresentativeY.ToString("F6", CultureInfo.InvariantCulture);

        _currentPoints = extractionResult.ProfilePoints
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        _pointRows.Clear();
        for (int index = 0; index < _currentPoints.Count; index++)
        {
            RailProfilePoint point = _currentPoints[index];
            _pointRows.Add(new MedianSectionPointDisplayItem(index + 1, point.X, point.Y));
        }

        RepresentativePointCountText = _currentPoints.Count.ToString(CultureInfo.InvariantCulture);

        if (_currentPoints.Count == 0)
        {
            XRangeText = "-";
            ZRangeText = "-";
        }
        else
        {
            double minX = _currentPoints.Min(point => point.X);
            double maxX = _currentPoints.Max(point => point.X);
            double minZ = _currentPoints.Min(point => point.Y);
            double maxZ = _currentPoints.Max(point => point.Y);

            XRangeText = $"{minX:F6} ~ {maxX:F6}";
            ZRangeText = $"{minZ:F6} ~ {maxZ:F6}";
        }

        OnPropertyChanged(nameof(CanExport));
    }

    private void ClearCurrentPoints()
    {
        _currentRepresentativeY = null;
        _currentPoints = new List<RailProfilePoint>();
        _pointRows.Clear();
        RepresentativeYText = "-";
        RepresentativePointCountText = "0";
        XRangeText = "-";
        ZRangeText = "-";
        OnPropertyChanged(nameof(CanExport));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
