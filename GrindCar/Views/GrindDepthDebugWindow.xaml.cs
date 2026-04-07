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

    public GrindDepthDebugWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GrindDepthResult> Results => _results;

    private async void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<int> angles = ParseAngles(AnglesTextBox.Text);
            ToggleBusyState(true);
            SetStatus($"正在计算 {angles.Count.ToString(CultureInfo.InvariantCulture)} 个角度的打磨深度...");

            IReadOnlyList<GrindDepthResult> results =
                await Task.Run(() => RailSurfaceService.GetGrindDepths(angles));

            _results.Clear();
            for (int index = 0; index < results.Count; index++)
            {
                _results.Add(results[index]);
            }

            ResultCountTextBlock.Text = results.Count.ToString(CultureInfo.InvariantCulture);
            SetStatus($"计算完成，共得到 {results.Count.ToString(CultureInfo.InvariantCulture)} 条结果。");
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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

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

    private void ToggleBusyState(bool isBusy)
    {
        _isBusy = isBusy;
        CalculateButton.IsEnabled = !_isBusy;
        AnglesTextBox.IsEnabled = !_isBusy;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
