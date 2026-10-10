using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using GrindCar.Models.Measurement;

namespace GrindCar.ViewModels;

public enum WorkflowStepState { Pending, Active, Completed, Failed, Canceled }

public sealed class WorkflowStepViewModel : INotifyPropertyChanged
{
    public MeasurementWorkflowStage Stage { get; }
    public string Title { get; }
    public string Number { get; }
    private WorkflowStepState _state;
    public WorkflowStepState State
    {
        get => _state;
        internal set
        {
            _state = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Symbol)));
        }
    }
    public string Symbol => State switch
    {
        WorkflowStepState.Completed => "✓",
        WorkflowStepState.Failed => "!",
        WorkflowStepState.Canceled => "−",
        _ => Number
    };
    public event PropertyChangedEventHandler? PropertyChanged;
    public WorkflowStepViewModel(MeasurementWorkflowStage stage, string title, string number)
    {
        Stage = stage;
        Title = title;
        Number = number;
    }
}

public sealed record DashboardMessage(DateTime Timestamp, string Text)
{
    public string TimeText => Timestamp.ToString("HH:mm:ss");
}

/// <summary>仅负责首页展示；所有更新在创建时的 UI 上下文执行。</summary>
public sealed class DashboardActivityViewModel : INotifyPropertyChanged
{
    public const int MessageLimit = 100;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly ObservableCollection<DashboardMessage> _messages = new();
    private readonly HashSet<MeasurementWorkflowStage> _visited = new();
    public ReadOnlyObservableCollection<DashboardMessage> Messages { get; }
    public IReadOnlyList<WorkflowStepViewModel> Steps { get; } = new[]
    {
        new WorkflowStepViewModel(MeasurementWorkflowStage.Idle, "待机", "1"),
        new WorkflowStepViewModel(MeasurementWorkflowStage.Positioning, "定位", "2"),
        new WorkflowStepViewModel(MeasurementWorkflowStage.Capturing, "采集", "3"),
        new WorkflowStepViewModel(MeasurementWorkflowStage.Calculating, "计算", "4"),
        new WorkflowStepViewModel(MeasurementWorkflowStage.Confirming, "确认", "5"),
        new WorkflowStepViewModel(MeasurementWorkflowStage.Writing, "写入", "6")
    };
    public MeasurementWorkflowStage CurrentStage { get; private set; } = MeasurementWorkflowStage.Idle;
    public string StageDescription { get; private set; } = "等待启动测量";
    public string CurrentMessage { get; private set; } = "等待操作";
    public bool IsRunning { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public DashboardActivityViewModel()
    {
        Messages = new ReadOnlyObservableCollection<DashboardMessage>(_messages);
        Steps[0].State = WorkflowStepState.Active;
    }

    public void Begin(bool offline = false) => OnUi(() =>
    {
        _visited.Clear();
        foreach (WorkflowStepViewModel step in Steps) step.State = WorkflowStepState.Pending;
        IsRunning = true;
        SetStageCore(offline ? MeasurementWorkflowStage.Calculating : MeasurementWorkflowStage.Idle);
    });

    public void SetStage(MeasurementWorkflowStage stage) => OnUi(() =>
    {
        if (IsRunning) SetStageCore(stage);
    });

    private void SetStageCore(MeasurementWorkflowStage stage)
    {
        CurrentStage = stage;
        _visited.Add(stage);
        foreach (WorkflowStepViewModel step in Steps)
            step.State = step.Stage == stage ? WorkflowStepState.Active :
                _visited.Contains(step.Stage) ? WorkflowStepState.Completed : WorkflowStepState.Pending;
        StageDescription = stage switch
        {
            MeasurementWorkflowStage.Positioning => "等待采集触发",
            MeasurementWorkflowStage.Capturing => "正在采集点云数据",
            MeasurementWorkflowStage.Calculating => "正在计算打磨深度",
            MeasurementWorkflowStage.Confirming => "等待查看廓形并确认打磨深度",
            MeasurementWorkflowStage.Writing => "正在写入打磨次数",
            _ => "正在准备测量"
        };
        Notify();
    }

    public void Complete() => Finish(WorkflowStepState.Completed, "打磨次数写入完成");
    public void Cancel() => Finish(WorkflowStepState.Canceled, "已取消写入 PLC");
    public void Fail() => Finish(WorkflowStepState.Failed, "流程异常，请查看运行消息");

    private void Finish(WorkflowStepState state, string description) => OnUi(() =>
    {
        if (!IsRunning) return;
        IsRunning = false;
        Steps[(int)CurrentStage].State = state;
        StageDescription = description;
        Notify();
    });

    public void AddMessage(string message) => OnUi(() =>
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        CurrentMessage = message;
        _messages.Insert(0, new DashboardMessage(DateTime.Now, message));
        while (_messages.Count > MessageLimit) _messages.RemoveAt(_messages.Count - 1);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentMessage)));
    });

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StageDescription)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
    }

    private void OnUi(Action action)
    {
        if (_context == null || SynchronizationContext.Current == _context) action();
        else _context.Send(_ => action(), null);
    }
}
