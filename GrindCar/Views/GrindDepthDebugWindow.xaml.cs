using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.Services;

namespace GrindCar.Views;

/// <summary>
/// 打磨深度调试窗口。
/// </summary>
public partial class GrindDepthDebugWindow : Window, INotifyPropertyChanged
{
    private readonly ObservableCollection<GrindDepthResult> _results = new();
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

    public ObservableCollection<GrindDepthResult> Results => _results;

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
            SetStatus($"正在计算 {angles.Count.ToString(CultureInfo.InvariantCulture)} 个角度的打磨深度...");

            GrindDepthCalculationResult calculationResult =
                await Task.Run(() => RailSurfaceService.CalculateGrindDepths(angles));

            IReadOnlyList<GrindDepthResult> results = calculationResult.Results;

            _results.Clear();
            for (int index = 0; index < results.Count; index++)
            {
                _results.Add(results[index]);
            }

            ResultCountTextBlock.Text = results.Count.ToString(CultureInfo.InvariantCulture);
            SetStatus($"计算完成，共得到 {results.Count.ToString(CultureInfo.InvariantCulture)} 条结果。");

            var comparisonWindow = new RepresentativeProfileComparisonWindow(calculationResult.RepresentativePoints)
            {
                Owner = this
            };
            comparisonWindow.Show();
        }
        catch (Exception ex)
        {
            _results.Clear();
            ResultCountTextBlock.Text = "0";
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "计算失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
        AnglesTextBox.IsEnabled = !_isBusy;
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
