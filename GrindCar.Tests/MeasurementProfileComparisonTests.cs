using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GrindCar.Models.Rail;
using GrindCar.Services.Measurement;
using GrindCar.Services.Rail.Debug;
using GrindCar.ViewModels;
using GrindCar.Views;
using Xunit;

namespace GrindCar.Tests;

public class MeasurementProfileComparisonTests
{
    private static RailProfilePoint[] Points(double offset = 0) => new[]
    {
        new RailProfilePoint(-30, -8 + offset), new RailProfilePoint(0, offset), new RailProfilePoint(30, -8 + offset)
    };

    [Fact]
    public void Snapshot_IsIndependentAndReadOnly()
    {
        var source = Points().ToList();
        var profile = new MeasurementRepresentativeProfile(1, source);
        source.Clear();
        Assert.Equal(3, profile.Points.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<RailProfilePoint>)profile.Points).Clear());
        Assert.Empty(new MeasurementGrindingWorkflowResult(1, Array.Empty<MeasurementGrindingTimesResult>()).RepresentativeProfiles);
    }

    [Fact]
    public void Groups_SwitchWithSharedAxes_ClearInvalidAndHidePoints()
    {
        var profiles = new[] { new MeasurementRepresentativeProfile(1, Points()),
            new MeasurementRepresentativeProfile(2, Points(2)),
            new MeasurementRepresentativeProfile(3, Array.Empty<RailProfilePoint>()) };
        var vm = new RepresentativeProfileComparisonViewModel(profiles);
        Assert.Same(profiles[0], vm.SelectedProfile);
        Assert.Equal(Visibility.Visible, vm.ProfileSelectorVisibility);
        vm.UpdatePlot(900, 500);
        double axis = vm.XAxisY1;
        Assert.False(vm.RepresentativeCurveGeometry.IsEmpty());
        vm.SelectedProfile = profiles[1];
        Assert.Equal(axis, vm.XAxisY1);
        Assert.Empty(vm.RepresentativePointItems);
        Assert.True(vm.LeftCurveGeometry.IsEmpty());
        Assert.Empty(vm.MaximumDropMarkerItems);
        vm.SelectedProfile = profiles[2];
        Assert.True(vm.RepresentativeCurveGeometry.IsEmpty());
        Assert.True(vm.StandardCurveGeometry.IsEmpty());
        Assert.Contains("第3组", vm.PlotError);
        vm.SelectedProfile = profiles[0];
        Assert.Empty(vm.PlotError);
        Assert.False(vm.RepresentativeCurveGeometry.IsEmpty());
        Assert.Equal(Visibility.Collapsed,
            new RepresentativeProfileComparisonViewModel(new[] { profiles[0] }).ProfileSelectorVisibility);
        Assert.False(new RepresentativeProfileComparisonViewModel(new[] { profiles[2] }).HasDrawableProfiles);
    }

    [Fact]
    public void Plot_CommonBounds_UsesEqualAxisScale()
    {
        var service = new RepresentativeProfileComparisonService();
        var snapshot = service.BuildSnapshot(new[] { new RailProfilePoint(0, 0), new RailProfilePoint(10, 10) });
        var plot = service.BuildPlot(snapshot, 900, 500, commonBounds: new RepresentativeProfileBounds(-30, 30, -15, 15));
        Assert.Equal(Math.Abs(plot.RepresentativePoints[1].Left - plot.RepresentativePoints[0].Left),
            Math.Abs(plot.RepresentativePoints[1].Top - plot.RepresentativePoints[0].Top), 8);
    }

    [Fact]
    public void Window_ThemesAndSizes_RenderComparison()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new RepresentativeProfileComparisonWindow(new[]
                {
                    new MeasurementRepresentativeProfile(1, Points()),
                    new MeasurementRepresentativeProfile(2, Points(1))
                });
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    { Source = new Uri($"/GrindCar;component/Themes/{theme}Theme.xaml", UriKind.Relative) });
                    foreach (Size size in new[] { new Size(980, 720), new Size(680, 440) })
                    {
                        var content = (FrameworkElement)window.Content;
                        content.Measure(size);
                        content.Arrange(new Rect(size));
                        content.UpdateLayout();
                        var vm = (RepresentativeProfileComparisonViewModel)window.DataContext;
                        vm.UpdatePlot(window.PlotCanvas.ActualWidth, window.PlotCanvas.ActualHeight);
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        content.UpdateLayout();
                        Assert.False(vm.StandardCurveGeometry.IsEmpty());
                        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(content);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        string directory = Path.Combine(AppContext.BaseDirectory, "ui-review");
                        Directory.CreateDirectory(directory);
                        using FileStream file = File.Create(Path.Combine(directory, $"measurement-profile-{theme}-{size.Width}.png"));
                        encoder.Save(file);
                    }
                }
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
    }
}
