using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GrindCar.Models.Measurement;
using GrindCar.Services.Measurement;
using GrindCar.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GrindCar.Views;
using Xunit;

namespace GrindCar.Tests;

public partial class GrindingWindowTests
{
    private static void VerifyDashboardLayout(MainWindow dashboard, FrameworkElement content, Size size, string theme)
    {
        var write = (Button)dashboard.FindName("WriteMeasurementParametersButton");
        var measure = (Button)dashboard.FindName("StartMeasurementMotionButton");
        var grind = (Button)dashboard.FindName("StartGrindingMotionButton");
        var start = (TextBox)dashboard.FindName("MeasurementStartPositionTextBox");
        string original = dashboard.Measurement.StartPositionText;
        start.Text = "1.25";
        Assert.Equal("1.25", dashboard.Measurement.StartPositionText);
        dashboard.Measurement.StartPositionText = original;
        Assert.False(write.IsEnabled);
        Assert.True(measure.IsEnabled);
        Assert.True(grind.IsEnabled);
        dashboard.Measurement.SetWorkflowActive(true);
        Assert.False(measure.IsEnabled);
        Assert.False(grind.IsEnabled);
        Assert.All(((Panel)dashboard.FindName("NavigationButtons")).Children.OfType<Button>(), button => Assert.False(button.IsEnabled));
        dashboard.Measurement.SetWorkflowActive(false);

        ScrollViewer horizontal = Descendants(content).OfType<ScrollViewer>()
            .Single(scroll => scroll.Content is Grid grid && grid.Name == "PageArea");
        ScrollViewer vertical = Descendants((FrameworkElement)dashboard.FindName("HomePage")).OfType<ScrollViewer>().First();
        horizontal.ScrollToRightEnd();
        vertical.ScrollToEnd();
        content.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        foreach (Button button in new[] { measure, grind, write })
        {
            Point origin = button.TranslatePoint(new Point(), content);
            Assert.InRange(origin.X, 0, size.Width);
            Assert.InRange(origin.X + button.ActualWidth, 0, size.Width);
            Assert.InRange(origin.Y, 0, size.Height);
            Assert.InRange(origin.Y + button.ActualHeight, 0, size.Height);
        }
        SaveDashboardRender(content, size, 96, $"dashboard-{theme}-{size.Width}-controls.png");
        horizontal.ScrollToLeftEnd();
        vertical.ScrollToTop();
        content.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        SaveDashboardRender(content, size, 144, $"dashboard-{theme}-{size.Width}-150percent.png");
    }

    private static void VerifyDashboardOfflineEntry(MainWindow dashboard)
    {
        var originalPlot = dashboard.Measurement.Profile.Plot;
        int? originalGroup = dashboard.Measurement.Profile.SampleIndex;
        Await(dashboard.NavigateAsync(MainPage.Validation));
        var view = Assert.IsType<PointCloudGrindDepthDebugView>(((ContentControl)dashboard.FindName("PageHost")).Content);
        var run = (Func<Window, Func<Task<MeasurementGrindingWorkflowResult>>, Task>)typeof(PointCloudGrindDepthDebugView)
            .GetField("_runWorkflowAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        Await(Assert.ThrowsAsync<InvalidOperationException>(() => run(dashboard,
            () => Task.FromException<MeasurementGrindingWorkflowResult>(new InvalidOperationException("离线计算测试失败")))));
        Assert.Equal(MeasurementWorkflowStage.Calculating, dashboard.Measurement.Activity.CurrentStage);
        Assert.Equal(WorkflowStepState.Failed, dashboard.Measurement.Activity.Steps[3].State);
        Assert.All(dashboard.Measurement.Activity.Steps.Take(3), step => Assert.Equal(WorkflowStepState.Pending, step.State));
        Assert.True(dashboard.Measurement.CanOperate);
        Assert.Same(originalPlot, dashboard.Measurement.Profile.Plot);
        Assert.Equal(originalGroup, dashboard.Measurement.Profile.SampleIndex);
        Await(dashboard.NavigateAsync(MainPage.Home));
    }

    private static void SaveDashboardRender(FrameworkElement content, Size size, double dpi, string name)
    {
        const double defaultDpi = 96;
        var bitmap = new RenderTargetBitmap((int)(size.Width * dpi / defaultDpi),
            (int)(size.Height * dpi / defaultDpi), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = Path.Combine(AppContext.BaseDirectory, "ui-review");
        Directory.CreateDirectory(directory);
        using FileStream file = File.Create(Path.Combine(directory, name));
        encoder.Save(file);
    }
}
