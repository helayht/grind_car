using System;
using System.Collections.Generic;

namespace GrindCar.Models.Rail;

/// <summary>
/// 表示“需要打磨深度计算”阶段保存的检测基线数据。
/// </summary>
/// <param name="CreatedAt">基线创建时间。</param>
/// <param name="Angles">本次基线使用的角度列表。</param>
/// <param name="BaselineBValues">各角度对应的基线 b 值。</param>
public sealed record GrindingDepthBaseline(
    DateTime CreatedAt,
    IReadOnlyList<int> Angles,
    IReadOnlyList<AngleBValue> BaselineBValues);
