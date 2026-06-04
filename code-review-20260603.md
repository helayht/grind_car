# 代码审查报告

**分支**: `20260331`  
**审查日期**: 2026-06-03  
**变更规模**: 23 commits, 109 files, +10,201 / −2,718 lines  
**审查范围**: `main...20260331`

---

## 审查概要

| 严重程度 | 数量 | 说明 |
|----------|------|------|
| 🔴 关键 | 2 | 硬编码 Modbus unitId，静默读写错误设备 |
| 🟠 高 | 1 | 异常被吞没，Y 轴可能失控运行 |
| 🟡 中 | 2 | 角度 90 数值精度丧失；空点云无 CSV 回退 |
| 🟢 低 | 2 | 预计算值被丢弃；公共字段无 volatile 保护 |
| ⚪ 清理 | 4 | 代码重复、O(n log n) 中位数、残留垃圾字符等 |

---

## 🔴 关键 (Critical)

### 1. `ReadSingleCoil` 硬编码 unitId=1，忽略构造函数参数 `_unitId`

**文件**: `GrindCar/Services/PlcModbusCommunicator.SingleReadWrite.cs`  
**行号**: 43

```csharp
// 第 43 行 — 应使用 _unitId 而非 1
bool[] response = modbusMaster.ReadCoils(1, coilAddress, 1);
```

**调用位置**:
- `MeasurementParameterService.cs:151,158` — 测量工作流中读取完成/触发信号
- `PlcModbusCommunicator.Motion.cs:93,105` — `GetData()` 方法

**影响**: 所有其他 Modbus 方法（`WriteSingleCoilAsync`、`WriteFloat`、`ReadFloat`、`ReadInt32` 等）均正确使用 `_unitId`。若 PLC 配置的 unit ID 非 1，将静默读取错误设备的线圈状态，导致测量工作流卡死或行为异常。

**修复**:
```csharp
// 将第 43 行的 1 改为 _unitId
bool[] response = modbusMaster.ReadCoils(_unitId, coilAddress, 1);
```

---

### 2. `WriteSingleCoil` 同样硬编码 unitId=1

**文件**: `GrindCar/Services/PlcModbusCommunicator.SingleReadWrite.cs`  
**行号**: 21

```csharp
// 第 21 行
modbusMaster.WriteSingleCoil(1, coilAddress, value);
```

**影响**: 同步版 `WriteSingleCoil` 目前无外部调用者，但异步版 `WriteSingleCoilAsync`（第 71 行）正确使用了 `_unitId`。此不一致是潜在的陷阱——任何未来的同步调用都会写入错误的从设备。

**修复**:
```csharp
modbusMaster.WriteSingleCoil(_unitId, coilAddress, value);
```

---

## 🟠 高风险 (High)

### 3. `MoveAndMonitorAsync` 异常被吞没，Y 轴可能失控运行

**文件**: `GrindCar/Services/PlcModbusCommunicator.Motion.cs`  
**行号**: 49–74

```csharp
try
{
    // Y 轴目标位置和速度写入成功
    WriteFloat(yPositionAddress, yTarget);
    WriteFloat(ySpeedAddress, commonSpeed);
    await WriteSingleCoilAsync(yRunFlagAddress, true);  // Y 轴开始物理运动！

    // X 轴目标位置写入失败 → 抛出 SocketException
    WriteFloat(xPositionAddress, xTarget);
    WriteFloat(xSpeedAddress, commonSpeed);
    await WriteSingleCoilAsync(xRunFlagAddress, true);

    StartContinuousCoilReading(xCompletionFlagAddress, 1, coilPollInterval);
    StartContinuousCoilReading2(yCompletionFlagAddress, 1, coilPollInterval);
}
catch (Exception ex)
{
    StopContinuousCoilReading();   // 监控任务未启动，无操作
    StopContinuousCoilReading2();  // 监控任务未启动，无操作
    // 静默返回 —— Y 轴继续失控移动，调用者认为操作成功
}
```

**失败场景**:  
Y 轴的 WriteFloat 和 WriteSingleCoilAsync 全部成功（Y 轴开始物理移动），随后 X 轴的 WriteFloat 抛出 SocketException。catch 块停止了从未启动的监控任务，并静默返回。调用者收到无错误指示，但 Y 轴继续物理移动，无任何监控或停止机制。

**修复**: 在 catch 块中添加紧急停止 Y 轴的操作，并重新抛出异常或将错误信息返回给调用者。

---

## 🟡 中风险 (Medium)

### 4. 角度 90° 导致 Tan 值溢出，打磨深度计算发生灾难性抵消

**文件**: `GrindCar/Services/RailSurfaceService.cs`  
**行号**: 225 (CalculateSlopeFromAngle)

配置的角度列表 (`MotorParameterDefinitions.cs:303`) 包含 **90°**:

```csharp
public static IReadOnlyList<int> MeasurementGrindingAngles { get; } = new List<int>
{
    -35, -20, -15, -10, -5, -2, 0, 2, 5, 10, 15, 25, 35, 45, 55, 65, 75, 83, 90
};
```

**问题分析**:

```csharp
double k = Math.Tan(90 * Math.PI / 180);  // ≈ 1.63 × 10¹⁶（有限值，非无穷大）
```

在 `GetRepresentativeB` 中:
```
candidate = point.Y - k * point.X
          ≈ (-176) - (1.6×10¹⁶ × ±35)
          ≈ ±5.7×10¹⁷
```

`point.Y`（约 −176）完全被 `k×X` 项淹没——所有 Y 精度在双精度浮点数中丧失。角度 90 的打磨深度结果在数值上不可靠。

**修复**: 对角度 90 进行特殊处理，直接使用轨面顶点差值计算；或从配置角度列表中移除 90°。

---

### 5. 在线采集返回空列表时不触发 CSV 回退

**文件**: `GrindCar/Services/Rail/PointCloudMedianSectionCaptureService.cs`  
**行号**: 86–103

```csharp
IReadOnlyList<PointCloudPoint3D>? points = null;
try
{
    points = _pointCloudExportService.CapturePointCloudPoints(serialNumber, captureSettings);
}
catch { /* 捕获异常时回退到 CSV */ }

if (points != null)          // ← 守卫只检查 null
{
    // 若 points 为空列表，此处抛出 RepresentativeProfileExtractionException
    representativeProfileService.ExtractMedianSectionProfileFromPoints(points, side);
    return new PointCloudMedianSectionCaptureResult(string.Empty, onlineExtractionResult);
}
// 第 108 行的 CSV 回退永远不会被执行
```

**失败场景**:  
`CapturePointCloudPoints` 返回一个空 `List<PointCloudPoint3D>`（SDK 返回 `DecodeFormat.PointCount <= 0`）。`points != null` 守卫通过，但 `ExtractMedianSectionProfileFromPoints` 因 `points.Count == 0` 抛出 `RepresentativeProfileExtractionException`。异常向上传播，不触发 CSV 回退。

**修复**:
```csharp
if (points != null && points.Count > 0)
```

---

## 🟢 低风险 (Low)

### 6. `WriteGrindingTimesAsync` 重新计算打磨次数，丢弃预计算值

**文件**: `GrindCar/Services/Measurement/MeasurementParameterService.cs`  
**行号**: 120

```csharp
int grindingTimes = CalculateGrindingTimes(result.AverageGrindDepth);
plcClient.WriteInt32(address, grindingTimes);
```

`MeasurementGrindingTimesResult` 中已有预计算的 `GrindingTimes` 字段（由 `CalculateGrindingTimes(ConfirmedDepth)` 计算得出），此处重新计算可能出现不一致。

**修复**: 直接使用 `result.GrindingTimes`。

---

### 7. 公共布尔字段缺少 volatile 保护

**文件**: `GrindCar/Services/PlcModbusCommunicator.cs`  
**行号**: 22–23

```csharp
public bool _isCompleted1;
public bool _isCompleted2;
```

这些字段在 `MoveAndMonitorAsync` 中由线程池线程写入，但声明为普通布尔值。任何其他线程的读取可能无限期看到过期值。当前无外部消费者，但公共可见性容易导致误用。

**修复**: 改为 `private volatile` 或使用 `Interlocked` 操作。

---

## ⚪ 代码清理 (Cleanup)

### 8. 残留垃圾字符

**文件**: `GrindCar/Converters/InverseBooleanToVisibilityConverter.cs`  
**行号**: 15

```csharp
/// </summary>sssssssssssssssssss
```

`</summary>` 标签后的 19 个 's' 字符破坏了 XML 文档注释，会导致 Sandcastle/DocFX 等文档生成工具报错。

**修复**: 删除残留字符。

---

### 9. CSV 读取工具方法重复

**文件**:  
- `GrindCar/Services/Curve/CurveCsvReader.cs`  
- `GrindCar/Services/Rail/Processing/PointCloudCsvReader.cs`

四个相同的工具方法在两处重复约 100 行:

| 方法 | CurveCsvReader | PointCloudCsvReader |
|------|---------------|---------------------|
| `DetectDelimiter` | ✅ | ✅ (相同) |
| `SplitLine` | ✅ | ✅ (相同) |
| `ReadFirstNonEmptyLine` | ✅ | ✅ (相同) |
| `TryParseDouble` | ✅ | ✅ (相同) |

任何 CSV 解析的 bug 修复或增强都需要在两处应用。

**修复**: 提取到共享的 `CsvReaderHelper` 类中。

---

### 10. CSV 导出工具方法重复

**文件**:  
- `GrindCar/Services/Rail/Debug/MedianSectionCsvExporter.cs`  
- `GrindCar/Services/Rail/Debug/RepresentativePointsCsvExporter.cs`

两个 45 行的类仅差异在表头和属性访问器（`point.X/point.Y` vs `point.Y`），共享相同的编码方式（UTF-8 无 BOM）、格式化（F6）、目录检查和错误消息。

**修复**: 合并为参数化的单一 `CsvExporter`。

---

### 11. 中位数计算使用 O(n log n) 全排序

**文件**: `GrindCar/Services/Rail/Processing/RepresentativeProfilePointProcessor.cs`  
**行号**: 790

私有 `Median` 方法对数组进行全排序来求中位数（O(n log n)），而代码库中已有 `Services/Rail/Processing/QuickSelect.cs`，可在 O(n) 时间内找到第 k 小元素。

**调用热路径**:
- `FilterOutlierRepresentativePointsSinglePass`（第 232 行）
- `SelectBestBoundaryInliers`（第 421 行）

对于大型点云，全排序浪费 CPU 资源。

**修复**: 用 `QuickSelect.SelectKthSmallest` 替换排序逻辑。

---

### 12. `ParsePosition` 与 `ParseDouble` 实现重复

**文件**: `GrindCar/Services/Measurement/MeasurementInputParser.cs`  
**行号**: 11, 76

`ParsePosition`（公有方法）与 `ParseDouble`（私有方法）实现完全相同：相同的 `double.TryParse` 回退链（先 CurrentCulture 后 InvariantCulture）和相同的错误消息格式。

**修复**: `ParsePosition` 委托给 `ParseDouble`。

---

## 架构观察

以下不属于缺陷，但值得注意：

1. **`RailProfileFittingService` 已完全删除**（441 行）——其功能（样条拟合、异常值去除、平滑）由新的 `StandardRailProfileSolver` + `RepresentativeProfilePointProcessor` 替代，使用了不同的算法（解析弧线方程替代样条拟合）。旧服务仅由已删除的测试窗口使用，无生产依赖。

2. **`PlcModbusCommunicator` 拆分为 partial class**——连接/断开、读写、连续读取、运动控制分离到独立文件中，代码组织得到改善。所有守卫、错误处理和常量均得以保留。

3. **`MotorViewModel` 大幅精简**（894 → 542 行）——写入规范和读取缩放因子提取到 `MotorParameterSpecProvider`，输入解析提取到 `MotorInputParser`。仪表盘面板取消模拟数据，改为静态占位符。

---

*审查工具: Claude Code code-review skill (high effort)*
