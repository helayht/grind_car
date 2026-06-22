using System;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 测量流程原始点云留档上下文。
/// </summary>
public sealed record MeasurementPointCloudArchiveContext(
    int SampleIndex,
    int DeviceIndex,
    string SerialNumber,
    PointCloudDeviceSide Side,
    IProgress<string>? Progress);
