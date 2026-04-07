using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 中位 Y 截面调试窗口。
/// </summary>
public partial class MedianSectionDebugWindow : Window, INotifyPropertyChanged
{
    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly IPointCloudRepresentativeProfileService _profileService = new PointCloudRepresentativeProfileService();
    private readonly ObservableCollection<MedianSectionPointDisplayItem> _pointRows = new();
    private List<RailProfilePoint> _currentPoints = new();
    private double? _currentMedianY;
    private string _importedFilePath = "未导入文件";
    private string _medianYText = "-";
    private string _representativePointCountText = "0";
    private string _xRangeText = "-";
    private string _zRangeText = "-";
    private string _statusText = "请选择一个 .csv 点云文件。";

    public MedianSectionDebugWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

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

    public string MedianYText
    {
        get => _medianYText;
        private set
        {
            if (_medianYText == value)
            {
                return;
            }

            _medianYText = value;
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

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择点云 CSV 文件",
            Filter = CsvFileFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string filePath = dialog.FileName;
        ImportedFilePath = filePath;
        StatusText = $"正在提取中位 Y 截面: {filePath}";

        try
        {
            MedianSectionExtractionResult extractionResult =
                await Task.Run(() => _profileService.ExtractMedianSectionProfileFromCsv(filePath));

            ApplyProfilePoints(extractionResult);
            StatusText = $"提取完成，共得到 {_currentPoints.Count.ToString(CultureInfo.InvariantCulture)} 个代表点。";
        }
        catch (RepresentativeProfileExtractionException ex)
        {
            ClearCurrentPoints();
            StatusText = ex.Message;
            MessageBox.Show(this, ex.Message, "提取失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            ClearCurrentPoints();
            const string message = "提取中位 Y 截面时发生未处理异常。";
            StatusText = $"{message} {ex.Message}";
            MessageBox.Show(this, $"{message}\n{ex.Message}", "提取失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPoints.Count == 0 || _currentMedianY is null)
        {
            MessageBox.Show(this, "当前没有可导出的代表截面数据。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出代表截面坐标",
            Filter = CsvFileFilter,
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = BuildDefaultExportFileName()
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ExportProfileToCsv(dialog.FileName);
            StatusText = $"代表截面坐标已导出: {dialog.FileName}";
            MessageBox.Show(this, "代表截面坐标导出完成。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            const string message = "导出代表截面坐标时发生异常。";
            StatusText = $"{message} {ex.Message}";
            MessageBox.Show(this, $"{message}\n{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyProfilePoints(MedianSectionExtractionResult extractionResult)
    {
        _currentMedianY = extractionResult.MedianY;
        MedianYText = extractionResult.MedianY.ToString("F6", CultureInfo.InvariantCulture);

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

        ExportButton.IsEnabled = _currentPoints.Count > 0;
    }

    private void ClearCurrentPoints()
    {
        _currentMedianY = null;
        _currentPoints = new List<RailProfilePoint>();
        _pointRows.Clear();
        MedianYText = "-";
        RepresentativePointCountText = "0";
        XRangeText = "-";
        ZRangeText = "-";
        ExportButton.IsEnabled = false;
    }

    private string BuildDefaultExportFileName()
    {
        string sourceFileName = string.IsNullOrWhiteSpace(ImportedFilePath) || ImportedFilePath == "未导入文件"
            ? "median_section"
            : Path.GetFileNameWithoutExtension(ImportedFilePath);

        string medianYText = _currentMedianY?.ToString("F6", CultureInfo.InvariantCulture) ?? "unknown";
        string safeMedianYText = medianYText.Replace('.', '_');
        return $"{sourceFileName}_median_section_{safeMedianYText}.csv";
    }

    private void ExportProfileToCsv(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("导出文件路径不能为空。", nameof(outputPath));
        }

        if (_currentMedianY is null)
        {
            throw new InvalidOperationException("当前未加载中位 Y 截面数据。");
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("导出文件目录无效。");
        }

        Directory.CreateDirectory(directoryPath);

        var builder = new StringBuilder();
        builder.AppendLine($"MedianY,{_currentMedianY.Value.ToString("F6", CultureInfo.InvariantCulture)}");
        builder.AppendLine("X,Z");

        for (int index = 0; index < _currentPoints.Count; index++)
        {
            RailProfilePoint point = _currentPoints[index];
            builder.Append(point.X.ToString("F6", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(point.Y.ToString("F6", CultureInfo.InvariantCulture));
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
