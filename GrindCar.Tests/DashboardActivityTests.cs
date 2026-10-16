using System.Linq;
using GrindCar.Models.Measurement;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class DashboardActivityTests
{
    [Fact]
    public void Stages_MultipleGroups_ReflectsCaptureCalculationCycle()
    {
        var activity = new DashboardActivityViewModel();
        activity.Begin();
        activity.SetStage(MeasurementWorkflowStage.Positioning);
        activity.SetStage(MeasurementWorkflowStage.Capturing);
        activity.SetStage(MeasurementWorkflowStage.Calculating);
        activity.SetStage(MeasurementWorkflowStage.Capturing);
        Assert.Equal(WorkflowStepState.Active, activity.Steps[2].State);
        Assert.Equal(WorkflowStepState.Completed, activity.Steps[3].State);
        Assert.Equal(WorkflowStepState.Pending, activity.Steps[4].State);
        activity.SetStage(MeasurementWorkflowStage.Confirming);
        activity.SetStage(MeasurementWorkflowStage.Writing);
        activity.Complete();
        Assert.All(activity.Steps, step => Assert.Equal(WorkflowStepState.Completed, step.State));
        Assert.False(activity.IsRunning);
        activity.SetStage(MeasurementWorkflowStage.Capturing);
        Assert.Equal(MeasurementWorkflowStage.Writing, activity.CurrentStage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Finish_FailureOrCancel_PreservesStageAndResetsOnNewRun(bool failure)
    {
        var activity = new DashboardActivityViewModel();
        activity.Begin();
        activity.SetStage(MeasurementWorkflowStage.Confirming);
        if (failure) activity.Fail(); else activity.Cancel();
        Assert.Equal(MeasurementWorkflowStage.Confirming, activity.CurrentStage);
        Assert.Equal(failure ? WorkflowStepState.Failed : WorkflowStepState.Canceled, activity.Steps[4].State);
        Assert.Equal(WorkflowStepState.Pending, activity.Steps[5].State);
        activity.Begin();
        Assert.True(activity.IsRunning);
        Assert.Equal(WorkflowStepState.Active, activity.Steps[0].State);
        Assert.All(activity.Steps.Skip(1), step => Assert.Equal(WorkflowStepState.Pending, step.State));
    }

    [Fact]
    public void Begin_Offline_DoesNotFabricateAcquisitionSteps()
    {
        var activity = new DashboardActivityViewModel();
        activity.Begin(offline: true);
        activity.SetStage(MeasurementWorkflowStage.Confirming);
        activity.SetStage(MeasurementWorkflowStage.Writing);
        activity.Complete();
        Assert.All(activity.Steps.Take(3), step => Assert.Equal(WorkflowStepState.Pending, step.State));
        Assert.All(activity.Steps.Skip(3), step => Assert.Equal(WorkflowStepState.Completed, step.State));
    }

    [Fact]
    public void Messages_ExceedLimit_KeepsNewestWithTimestamps()
    {
        var activity = new DashboardActivityViewModel();
        for (int index = 0; index <= DashboardActivityViewModel.MessageLimit; index++)
            activity.AddMessage($"消息 {index}");
        Assert.Equal(DashboardActivityViewModel.MessageLimit, activity.Messages.Count);
        Assert.Equal("消息 100", activity.Messages[0].Text);
        Assert.Equal("消息 1", activity.Messages[^1].Text);
        Assert.Equal(activity.Messages[0].Text, activity.CurrentMessage);
        Assert.All(activity.Messages, message => Assert.NotEqual(default, message.Timestamp));
        activity.Begin();
        Assert.Equal(DashboardActivityViewModel.MessageLimit, activity.Messages.Count);
    }
}
