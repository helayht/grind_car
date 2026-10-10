namespace GrindCar.Models.Measurement;

/// <summary>测量进度展示阶段，不参与 PLC 控制决策。</summary>
public enum MeasurementWorkflowStage
{
    Idle,
    Positioning,
    Capturing,
    Calculating,
    Confirming,
    Writing
}
