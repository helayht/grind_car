using System;
using System.Threading.Tasks;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class MeasurementPointCloudArchiveServiceTests
{
    [Fact]
    public async Task QueueArchive_ExporterFails_ReportsProgressWithoutThrowing()
    {
        var progressSource = new TaskCompletionSource<string>();
        var progress = new Progress<string>(message => progressSource.TrySetResult(message));
        var context = new MeasurementPointCloudArchiveContext(
            1,
            2,
            "SN-001",
            PointCloudDeviceSide.Left,
            progress);

        MeasurementPointCloudArchiveService.QueueArchive(context, Array.Empty<PointCloudPoint3D>());

        Task completedTask = await Task.WhenAny(progressSource.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(progressSource.Task, completedTask);
        Assert.Contains("点云留档失败", await progressSource.Task);
    }
}
