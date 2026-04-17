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
using GrindCar.Services;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 打磨深度调试窗口。
/// </summary>
public partial class GrindDepthDebugWindow : Window, INotifyPropertyChanged
{
    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly ObservableCollection<GrindDepthResult> _requiredResults = new();
    private readonly ObservableCollection<DetectedGrindDepthResult> _detectedResults = new();
    private List<RailProfilePoint> _latestRepresentativePoints = new();
    private bool _isBusy;

    /// <summary>
    /// 初始化打磨深度调试窗口并绑定当前窗口为数据上下文。
    /// </summary>
    public GrindDepthDebugWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GrindDepthResult> RequiredResults => _requiredResults;

    public ObservableCollection<DetectedGrindDepthResult> DetectedResults => _detectedResults;

    /// <summary>
    /// 解析输入角度并执行打磨深度计算，同时展示结果窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private async void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<int> angles = ParseAngles(AnglesTextBox.Text);
            ToggleBusyState(true);
            SetStatus($"正在计算 {angles.Count.ToString(CultureInfo.InvariantCulture)} 个角度的需要打磨深度，并保存检测基线...");

            GrindDepthCalculationResult calculationResult =
                await Task.Run(() => RailSurfaceService.CalculateGrindDepths(angles));
            _latestRepresentativePoints = calculationResult.RepresentativePoints.ToList();
            GrindingDepthBaseline baseline =
                RailSurfaceService.CreateGrindingDepthBaseline(angles, calculationResult.RepresentativePoints);
            await Task.Run(() => RailSurfaceService.SaveGrindingDepthBaseline(baseline));

            IReadOnlyList<GrindDepthResult> results = calculationResult.Results;

            _requiredResults.Clear();
            for (int index = 0; index < results.Count; index++)
            {
                _requiredResults.Add(results[index]);
            }

            ResultCountTextBlock.Text = results.Count.ToString(CultureInfo.InvariantCulture);
            SetStatus($"计算完成，共得到 {results.Count.ToString(CultureInfo.InvariantCulture)} 条需要打磨深度结果，检测基线已更新。");

            var comparisonWindow = new RepresentativeProfileComparisonWindow(calculationResult.RepresentativePoints)
            {
                Owner = this
            };
            comparisonWindow.Show();
            UpdateExportButtonState();
        }
        catch (Exception ex)
        {
            _latestRepresentativePoints = new List<RailProfilePoint>();
            _requiredResults.Clear();
            ResultCountTextBlock.Text = "0";
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "计算失败", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateExportButtonState();
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    /// <summary>
    /// 加载基线并检测机器已打磨深度。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private async void Detect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ToggleBusyState(true);
            SetStatus("正在加载检测基线并检测已打磨深度...");

            GrindingDepthBaseline? baseline = await Task.Run(() => RailSurfaceService.LoadLatestGrindingDepthBaseline());
            if (baseline == null)
            {
                throw new InvalidOperationException("未找到检测基线，请先执行“计算需要打磨深度”。");
            }

            IReadOnlyList<DetectedGrindDepthResult> results =
                await Task.Run(() => RailSurfaceService.DetectGrindingDepths(baseline));

            _detectedResults.Clear();
            for (int index = 0; index < results.Count; index++)
            {
                _detectedResults.Add(results[index]);
            }

            ResultCountTextBlock.Text = results.Count.ToString(CultureInfo.InvariantCulture);
            SetStatus(
                $"检测完成，基线时间 {baseline.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)}，共检测 {results.Count.ToString(CultureInfo.InvariantCulture)} 个角度。");
        }
        catch (Exception ex)
        {
            _detectedResults.Clear();
            ResultCountTextBlock.Text = "0";
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "检测失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    /// <summary>
    /// 关闭当前窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 导出最近一次打磨深度计算所使用的代表点坐标。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void ExportRepresentativePoints_Click(object sender, RoutedEventArgs e)
    {
        if (_latestRepresentativePoints.Count == 0)
        {
            MessageBox.Show(this, "当前没有可导出的代表点，请先执行“计算需要打磨深度”。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出代表点坐标",
            Filter = CsvFileFilter,
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"representative-points-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ExportRepresentativePointsToCsv(dialog.FileName);
            SetStatus($"代表点导出完成: {dialog.FileName}");
            MessageBox.Show(this, "代表点导出完成。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetStatus($"导出代表点失败: {ex.Message}");
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 将界面输入文本解析为角度整数列表。
    /// </summary>
    /// <param name="input">用户输入的角度文本。</param>
    /// <returns>解析后的角度集合。</returns>
    private static IReadOnlyList<int> ParseAngles(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("请输入至少一个打磨角度。");
        }

        string normalizedText = input
            .Replace('，', ',')
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        string[] tokens = normalizedText
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("请输入至少一个有效的打磨角度。");
        }

        var angles = new List<int>(tokens.Length);
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int angle) &&
                !int.TryParse(token, NumberStyles.Integer, CultureInfo.CurrentCulture, out angle))
            {
                throw new InvalidOperationException($"无法解析角度值: {token}");
            }

            angles.Add(angle);
        }

        return angles;
    }

    /// <summary>
    /// 切换窗口控件的忙碌状态，避免重复提交计算。
    /// </summary>
    /// <param name="isBusy">是否处于忙碌状态。</param>
    private void ToggleBusyState(bool isBusy)
    {
        _isBusy = isBusy;
        CalculateButton.IsEnabled = !_isBusy;
        DetectButton.IsEnabled = !_isBusy;
        AnglesTextBox.IsEnabled = !_isBusy;
        UpdateExportButtonState();
    }

    /// <summary>
    /// 根据当前状态刷新导出按钮可用性。
    /// </summary>
    private void UpdateExportButtonState()
    {
        ExportRepresentativePointsButton.IsEnabled = !_isBusy && _latestRepresentativePoints.Count > 0;
    }

    /// <summary>
    /// 将代表点写入 CSV 文件（X,Y）。
    /// </summary>
    /// <param name="outputPath">目标输出路径。</param>
    private void ExportRepresentativePointsToCsv(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("导出路径不能为空。");
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("导出路径无效。");
        }

        Directory.CreateDirectory(directoryPath);

        var builder = new StringBuilder();
        builder.AppendLine("X,Y");
        for (int index = 0; index < _latestRepresentativePoints.Count; index++)
        {
            RailProfilePoint point = _latestRepresentativePoints[index];
            builder.Append(point.X.ToString("F6", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(point.Y.ToString("F6", CultureInfo.InvariantCulture));
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// 更新窗口状态提示文本。
    /// </summary>
    /// <param name="message">要显示的状态消息。</param>
    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    /// <summary>
    /// 触发属性变更通知，更新界面绑定。
    /// </summary>
    /// <param name="propertyName">发生变化的属性名称。</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
