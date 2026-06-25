using System;
using System.Collections.Generic;
using System.Windows;
using GrindCar.ViewModels;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 代表廓形手动配准窗口。
/// </summary>
public partial class ProfileRegistrationWindow : Window
{
    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly ProfileRegistrationViewModel _viewModel = new();

    public ProfileRegistrationWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += ProfileRegistrationWindow_Loaded;
    }

    private void ProfileRegistrationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdatePlot(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight);
    }

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.UpdatePlot(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight);
    }

    private void SelectLeftCsv_Click(object sender, RoutedEventArgs e)
    {
        string? filePath = SelectCsvFile("选择 Left 原始点云 CSV 文件");
        if (filePath == null)
        {
            return;
        }

        TryExecute(() => _viewModel.SetLeftFilePath(filePath), "加载 Left CSV 失败");
    }

    private void SelectRightCsv_Click(object sender, RoutedEventArgs e)
    {
        string? filePath = SelectCsvFile("选择 Right 原始点云 CSV 文件");
        if (filePath == null)
        {
            return;
        }

        TryExecute(() => _viewModel.SetRightFilePath(filePath), "加载 Right CSV 失败");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetParameters();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyPendingParameters();
    }

    private void ClearLeftXRange_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PendingLeftXMin = null;
        _viewModel.PendingLeftXMax = null;
    }

    private void ClearRightXRange_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PendingRightXMin = null;
        _viewModel.PendingRightXMax = null;
    }

    private void AutoAlignLeft_Click(object sender, RoutedEventArgs e)
    {
        TryExecute(() => _viewModel.AutoAlignLeft(), "Left 自动精对齐失败");
    }

    private void AutoAlignRight_Click(object sender, RoutedEventArgs e)
    {
        TryExecute(() => _viewModel.AutoAlignRight(), "Right 自动精对齐失败");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        TryExecute(
            () =>
            {
                _viewModel.SaveSettings();
                MessageBox.Show(this, "代表廓形配准参数已保存。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            },
            "保存配准参数失败");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private void TryExecute(Action action, string title)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            string message = BuildDetailedMessage(ex);
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
