using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GrindCar.Models.Rail;
using GrindCar.Services.Measurement;
using GrindCar.Services.Rail.Debug;
using GrindCar.ViewModels;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// Left/Right 原始点云算法验证页面。
/// </summary>
public partial class PointCloudGrindDepthDebugView : UserControl
{
    public event EventHandler? ReturnHomeRequested;

    private const string CsvFileFilter = "CSV 文件|*.csv";
    private readonly PointCloudGrindDepthDebugViewModel _viewModel = new();
    private readonly Func<Window, Func<Task<MeasurementGrindingWorkflowResult>>, Task> _runWorkflowAsync;
    private bool _workflowActive;

    public PointCloudGrindDepthDebugView(
        Func<Window, Func<Task<MeasurementGrindingWorkflowResult>>, Task> runWorkflowAsync)
    {
        _runWorkflowAsync = runWorkflowAsync ?? throw new ArgumentNullException(nameof(runWorkflowAsync));
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
        if (_workflowActive) return;
        try
        {
            _workflowActive = true;
            ((UIElement)Content).IsEnabled = false;
            await _runWorkflowAsync(Window.GetWindow(this), async () =>
                (await _viewModel.CalculateAsync()).ToMeasurementResult());
        }
        catch (Exception ex)
        {
            string message = BuildDetailedMessage(ex);
            _viewModel.SetErrorStatus(message);
            MessageBox.Show(Window.GetWindow(this), message, "计算失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _workflowActive = false;
            ((UIElement)Content).IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ReturnHomeRequested?.Invoke(this, EventArgs.Empty);
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
            Owner = Window.GetWindow(this)
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

        return dialog.ShowDialog(Window.GetWindow(this)) == true
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
