using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GrindCar.Models.Measurement;
using GrindCar.Models.Rail;
using GrindCar.Services.Measurement;
using GrindCar.Services;
using GrindCar.Services.PointCloud;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class DashboardProfileTests
{
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<Action> _queue = new();
        public override void Post(SendOrPostCallback callback, object? state) => _queue.Enqueue(() => callback(state));
        public void Drain() { while (_queue.Count > 0) _queue.Dequeue()(); }
    }

    private static MeasurementRepresentativeProfile Group(int index, double offset = 0) => new(index, new[]
    {
        new RailProfilePoint(-35.4, -8 + offset), new RailProfilePoint(0, offset), new RailProfilePoint(35.4, -8 + offset)
    });

    [Fact]
    public void StandardCurve_BeforeMeasurementAndAfterReset_RemainsVisible()
    {
        var context = new QueuedContext();
        var vm = new DashboardProfileViewModel(_ => { }, context);
        vm.Resize(700, 220);
        Assert.False(vm.Plot.StandardCurveGeometry.IsEmpty());
        Assert.True(vm.Plot.RepresentativeCurveGeometry.IsEmpty());
        string standard = vm.Plot.StandardCurveGeometry.ToString();
        long run = vm.Begin(RailSurfaceService.CurrentProfileType);
        Assert.Equal(standard, vm.Plot.StandardCurveGeometry.ToString());
        vm.CreateProgress(run).Report(Group(1));
        context.Drain();
        vm.Reset(RailSurfaceService.CurrentProfileType);
        Assert.False(vm.Plot.StandardCurveGeometry.IsEmpty());
        Assert.True(vm.Plot.RepresentativeCurveGeometry.IsEmpty());
        Assert.Null(vm.SampleIndex);
    }

    [Fact]
    public void Receive_SameGroupAsComparison_ProducesIdenticalCurvesAndEqualScale()
    {
        var context = new QueuedContext();
        var vm = new DashboardProfileViewModel(_ => { }, context);
        vm.Resize(700, 220);
        long run = vm.Begin(RailProfileType.Kg60);
        MeasurementRepresentativeProfile group = Group(1);
        vm.CreateProgress(run).Report(group);
        Assert.Null(vm.SampleIndex); // Notification must not render inline on the producer thread.
        context.Drain();
        var comparison = new RepresentativeProfileComparisonViewModel(new[] { group });
        comparison.UpdatePlot(700, 220);
        Assert.Equal(comparison.RepresentativeCurveGeometry.ToString(), vm.Plot.RepresentativeCurveGeometry.ToString());
        Assert.Equal(comparison.StandardCurveGeometry.ToString(), vm.Plot.StandardCurveGeometry.ToString());
        var bounds = vm.Plot.Bounds!.Value;
        Assert.Equal((bounds.MaxX - bounds.MinX) / (vm.Plot.XAxisX2 - vm.Plot.XAxisX1),
            (bounds.MaxY - bounds.MinY) / (vm.Plot.YAxisY2 - vm.Plot.YAxisY1), 8);
        Assert.Equal(Visibility.Collapsed, vm.EmptyVisibility);
    }

    [Fact]
    public void Receive_MultipleGroups_ExpandsBoundsAndKeepsFinalGroup()
    {
        var context = new QueuedContext();
        var vm = new DashboardProfileViewModel(_ => { }, context);
        vm.Resize(700, 220);
        long run = vm.Begin(RailProfileType.Kg60);
        var progress = vm.CreateProgress(run);
        progress.Report(Group(1, 20));
        context.Drain();
        var previous = vm.AccumulatedBounds;
        progress.Report(Group(2));
        vm.End(run, failed: false);
        context.Drain();
        Assert.Equal(2, vm.SampleIndex);
        Assert.True(vm.AccumulatedBounds!.Value.MinY <= previous!.Value.MinY);
        Assert.Equal(previous.Value.MaxY, vm.AccumulatedBounds.Value.MaxY);
        previous = vm.AccumulatedBounds;
        Assert.Contains("测量结束", vm.StatusText);
        DateTime? timestamp = vm.UpdatedAt;
        progress.Report(Group(3));
        context.Drain();
        Assert.Equal(2, vm.SampleIndex);
        Assert.Equal(timestamp, vm.UpdatedAt);
        vm.Resize(420, 180);
        Assert.False(vm.Plot.RepresentativeCurveGeometry.IsEmpty());
        Assert.Equal(previous, vm.AccumulatedBounds);
    }

    [Fact]
    public void Receive_InvalidGroup_KeepsPreviousGroupAndLogsWithoutThrowing()
    {
        var context = new QueuedContext();
        var messages = new List<string>();
        var vm = new DashboardProfileViewModel(messages.Add, context);
        vm.Resize(700, 220);
        long run = vm.Begin(RailProfileType.Kg60);
        var progress = vm.CreateProgress(run);
        progress.Report(Group(1));
        context.Drain();
        var plot = vm.Plot;
        var timestamp = vm.UpdatedAt;
        progress.Report(new MeasurementRepresentativeProfile(2, new[] { new RailProfilePoint(double.NaN, 0) }));
        vm.SetStage(run, MeasurementWorkflowStage.Capturing);
        context.Drain();
        Assert.Same(plot, vm.Plot);
        Assert.Equal(1, vm.SampleIndex);
        Assert.Equal(timestamp, vm.UpdatedAt);
        Assert.Single(messages);
        Assert.Contains("第 2 组显示失败", vm.StatusText);
        progress.Report(Group(3));
        context.Drain();
        Assert.Equal(3, vm.SampleIndex);
        Assert.DoesNotContain("失败", vm.StatusText);
        vm.End(run, failed: true);
        context.Drain();
        Assert.Contains("测量中断", vm.StatusText);
        Assert.Equal(3, vm.SampleIndex);
    }

    [Fact]
    public void ResetOrBegin_QueuedOldCallbacks_CannotOverwriteNewRun()
    {
        var context = new QueuedContext();
        var vm = new DashboardProfileViewModel(_ => { }, context);
        long oldRun = vm.Begin(RailProfileType.Kg60);
        vm.CreateProgress(oldRun).Report(Group(8));
        vm.End(oldRun, failed: true);
        long run = vm.Begin(RailProfileType.Kg50);
        context.Drain();
        Assert.Null(vm.SampleIndex);
        Assert.Equal("50 kg/m", vm.ProfileTypeText);
        Assert.DoesNotContain("中断", vm.StatusText);
        vm.CreateProgress(run).Report(Group(1));
        vm.Reset(RailProfileType.Kg60);
        context.Drain();
        Assert.Null(vm.SampleIndex);
        Assert.Null(vm.UpdatedAt);
        Assert.True(vm.Plot.RepresentativeCurveGeometry.IsEmpty());
    }

    [Fact]
    public void Receive_FirstGroupInvalid_ShowsErrorAndRecovers()
    {
        var context = new QueuedContext();
        var vm = new DashboardProfileViewModel(_ => { }, context);
        long run = vm.Begin(RailProfileType.Kg60);
        var progress = vm.CreateProgress(run);
        progress.Report(new MeasurementRepresentativeProfile(1, Array.Empty<RailProfilePoint>()));
        context.Drain();
        Assert.Contains("显示失败", vm.EmptyText);
        Assert.Equal(Visibility.Visible, vm.EmptyVisibility);
        progress.Report(Group(2));
        context.Drain();
        vm.Resize(600, 220);
        Assert.Equal(Visibility.Collapsed, vm.EmptyVisibility);
        Assert.False(vm.Plot.RepresentativeCurveGeometry.IsEmpty());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MeasurementViewModel_ActualCallbackWiring_UpdatesAndRetainsOnFailure(bool fail)
    {
        SynchronizationContext? original = SynchronizationContext.Current;
        var context = new QueuedContext();
        SynchronizationContext.SetSynchronizationContext(context);
        string directory = Path.Combine(Path.GetTempPath(), "dashboard-profile-" + Guid.NewGuid());
        try
        {
            using var connection = new SharedPlcConnectionService("127.0.0.1", 502, 1);
            using var vm = new MainWindowMeasurementViewModel(new WorkflowStub(fail), connection, "127.0.0.1", 502,
                new PointCloudCaptureSettingsStore(Path.Combine(directory, "capture.json")),
                new MeasurementPositionSettingsStore(Path.Combine(directory, "positions.json")))
            { CarSpeedText = "6", ProfileCountText = "100" };
            vm.Profile.Resize(700, 220);
            Task<MeasurementGrindingWorkflowResult> task = vm.StartMeasurementMotionAsync();
            if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            else Assert.Equal(1, (await task).SampleCount);
            context.Drain();
            Assert.Equal(1, vm.Profile.SampleIndex);
            Assert.False(vm.Profile.Plot.RepresentativeCurveGeometry.IsEmpty());
            Assert.Contains(fail ? "测量中断" : "测量结束", vm.Profile.StatusText);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class WorkflowStub : IMeasurementParameterService
    {
        private readonly bool _fail;
        public WorkflowStub(bool fail) => _fail = fail;
        public Task<MeasurementGrindingWorkflowResult> RunMeasurementWorkflowAsync(string ipAddress, int port,
            IProgress<string>? progress = null, Action? measurementEnded = null, CancellationToken cancellationToken = default,
            IProgress<MeasurementWorkflowStage>? stageProgress = null, IProgress<MeasurementRepresentativeProfile>? profileProgress = null)
        {
            stageProgress?.Report(MeasurementWorkflowStage.Capturing);
            profileProgress?.Report(Group(1));
            return _fail ? Task.FromException<MeasurementGrindingWorkflowResult>(new InvalidOperationException("模拟采集失败"))
                : Task.FromResult(new MeasurementGrindingWorkflowResult(1, Array.Empty<MeasurementGrindingTimesResult>()));
        }
        public Task WriteMeasurementParametersAsync(string ipAddress, int port, MeasurementParameters parameters) => throw new NotSupportedException();
        public Task WriteMeasurementRangeAsync(string ipAddress, int port, double startPosition, double endPosition) => throw new NotSupportedException();
        public Task WriteGrindingRangeAsync(string ipAddress, int port, double startPosition, double endPosition) => throw new NotSupportedException();
        public Task StartMeasurementMotionAsync(string ipAddress, int port) => throw new NotSupportedException();
        public Task StartGrindingMotionAsync(string ipAddress, int port) => throw new NotSupportedException();
        public Task WriteGrindingTimesAsync(string ipAddress, int port, IReadOnlyList<MeasurementGrindingTimesResult> results) => throw new NotSupportedException();
    }
}
