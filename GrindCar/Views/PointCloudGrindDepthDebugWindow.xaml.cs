using System;
using System.Collections.Generic;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
using GrindCar.ViewModels;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// Left/Right 原始点云算法验证窗口。
/// </summary>
public partial class PointCloudGrindDepthDebugWindow : Window
{
    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly PointCloudGrindDepthDebugViewModel _viewModel = new();

    public PointCloudGrindDepthDebugWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void SelectLeftCsv_Click(object sender, RoutedEventArgs e)
    {
        string? filePath = SelectCsvFile("选择 Left 原始点云 CSV 文件");
        if (filePath != null)
        {
            _viewModel.SetLeftFilePath(filePath);
        }
    }

    private void SelectRightCsv_Click(object sender, RoutedEventArgs e)
    {
        string? filePath = SelectCsvFile("选择 Right 原始点云 CSV 文件");
        if (filePath != null)
        {
            _viewModel.SetRightFilePath(filePath);
        }
    }

    private async void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<RailProfilePoint> representativePoints = await _viewModel.CalculateAsync();
            if (representativePoints.Count == 0)
            {
                return;
            }

            var comparisonWindow = new RepresentativeProfileComparisonWindow(representativePoints)
            {
                Owner = this
            };
            comparisonWindow.Show();
        }
        catch (Exception ex)
        {
            string message = BuildDetailedMessage(ex);
            _viewModel.SetErrorStatus(message);
            MessageBox.Show(this, message, "计算失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ViewMaximumDropProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LatestLeftMaximumDropProfile == null &&
            _viewModel.LatestRightMaximumDropProfile == null)
        {
            return;
        }

        var comparisonWindow = new RepresentativeProfileComparisonWindow(
            _viewModel.LatestLeftMaximumDropProfile,
            _viewModel.LatestRightMaximumDropProfile)
        {
            Owner = this
        };
        comparisonWindow.Show();
    }

    private string? SelectCsvFile(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = CsvFileFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(this) == true
            ? dialog.FileName
            : null;
    }

    private static string BuildDetailedMessage(Exception exception)
    {
        var messages = new List<string>();
        Exception? current = exception;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                !messages.Contains(current.Message))
            {
                messages.Add(current.Message);
            }

            current = current.InnerException;
        }

        return string.Join(Environment.NewLine, messages);
    }
}
