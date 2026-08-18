using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using GrindCar.Services.Measurement;
using GrindCar.Models.Rail;
using GrindCar.ViewModels;

namespace GrindCar.Views;

/// <summary>
/// 打磨深度确认窗口。
/// </summary>
public partial class GrindDepthConfirmationWindow : Window
{
    public GrindDepthConfirmationWindow(IReadOnlyList<MeasurementGrindingTimesResult> results)
        : this(results, null)
    {
    }

    public GrindDepthConfirmationWindow(
        IReadOnlyList<MeasurementGrindingTimesResult> results,
        MaximumDropProfileResult? maximumDropProfile)
    {
        InitializeComponent();
        Items = new ObservableCollection<GrindDepthConfirmationItemViewModel>(
            results.Select(result => new GrindDepthConfirmationItemViewModel(result)));
        SummaryText = BuildSummaryText(Items.Count, maximumDropProfile);
        DataContext = this;
    }

    public ObservableCollection<GrindDepthConfirmationItemViewModel> Items { get; }

    public string SummaryText { get; }

    public IReadOnlyList<MeasurementGrindingTimesResult> ConfirmedResults { get; private set; } =
        Array.Empty<MeasurementGrindingTimesResult>();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var confirmedResults = new List<MeasurementGrindingTimesResult>(Items.Count);

        for (int index = 0; index < Items.Count; index++)
        {
            GrindDepthConfirmationItemViewModel item = Items[index];
            if (!item.ValidateConfirmedDepth())
            {
                MessageBox.Show(
                    this,
                    $"角度 {item.Angle.ToString(CultureInfo.CurrentCulture)} 的确认打磨深度无效：{item.ErrorMessage}",
                    "输入无效",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            confirmedResults.Add(item.ToConfirmedResult());
        }

        ConfirmedResults = confirmedResults;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string BuildSummaryText(
        int angleCount,
        MaximumDropProfileResult? maximumDropProfile)
    {
        string angleText = $"共 {angleCount.ToString(CultureInfo.CurrentCulture)} 个角度";
        if (maximumDropProfile == null)
        {
            return angleText;
        }

        return $"{angleText}；最大掉块 {maximumDropProfile.MaximumDropDepth.ToString("F3", CultureInfo.CurrentCulture)} mm，" +
               $"来源 {maximumDropProfile.Side}，第 {maximumDropProfile.SampleIndex.ToString(CultureInfo.CurrentCulture)} 组，" +
               $"Y={maximumDropProfile.ProfileY.ToString("F3", CultureInfo.CurrentCulture)} mm";
    }
}
