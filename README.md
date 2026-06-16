# GrindCar

## 项目简介
GrindCar 是一个基于 `.NET 6 + WPF` 的轨面打磨设备上位机项目，面向本地桌面场景，当前主要覆盖以下能力：

- 驾驶舱首页展示电量、速度、连接状态等运行信息
- 电机调试窗口通过 `Modbus TCP` 与 PLC 建立连接，完成参数轮询、写入和调试
- 点云调试链路支持设备枚举、3D 点云模式单帧导出、在线点云平均代表截面提取（失败时自动回退 CSV）、需要打磨深度计算与已打磨深度检测
- 主界面内置参数设置，可写入“测量起点位置/测量终点位置”与“打磨起点位置/打磨终点位置”，并可配置点云在线采集速度、单次测量总条数和计算帧率；PLC 连接参数通过独立窗口维护
- 主界面支持测量运行流程：按 `M31/M60/M61/M62` 交互，两台廓形仪按顺序采集并合并为一组测量，累计多组后计算平均打磨深度并写回各角度打磨次数；并支持独立写入打磨运动启动 `M32`
- 轨面服务层提供标准轨面函数、代表截面提取、多角度打磨深度计算和按角度计算代表廓形 `b` 值的能力

该项目当前不依赖 Web 服务，也不依赖环境变量配置，主要作为本地运行的 HMI/调试工具使用。

## 技术栈
- `.NET 6`
- `WPF`
- `C# 10`
- `NModbus4 3.0.0-alpha1`
- `Mv3dLpNet.dll` 点云设备 SDK

## 项目结构
```text
GrindCar.sln
├─ GrindCar/
│  ├─ App.xaml
│  ├─ Definitions/        # 参数名称、地址、比例、单位定义
│  ├─ Infrastructure/     # 基础设施，例如 RelayCommand
│  ├─ Models/             # 业务模型
│  │  ├─ PointCloud/      # 点云设备、导出格式模型
│  │  └─ Rail/            # 轨面截面、计算结果模型
│  ├─ Services/           # PLC、轨面、点云业务服务
│  │  ├─ Motor/           # 电机参数映射、连接参数校验、仪表盘数据服务
│  │  ├─ Measurement/     # 主界面测量参数解析与写入服务
│  │  ├─ PointCloud/      # 点云设备 SDK 封装与导出
│  │  └─ Rail/            # 平均代表截面与代表廓形提取
│  │     ├─ Core/         # 轨面求解、基线存储、代表截面采集核心服务
│  │     └─ Processing/   # 代表廓形点集处理与 CSV 解析
│  ├─ ViewModels/         # ViewModel
│  ├─ Views/              # WPF 窗口
│  ├─ Converters/         # XAML 转换器
│  ├─ lib/                # 第三方本地 DLL
│  └─ Doc/                # 项目文档
├─ GrindCar.Tests/        # xUnit 单元测试项目
├─ global.json
└─ README.md
```

## 核心功能
### 1. 驾驶舱首页
- 启动后默认进入首页
- 展示电量、速度、连接状态和当前时间
- 首页中的电量和速度当前为“未接入数据”占位展示
- 可从首页进入点云导出、打磨深度调试、点云算法验证和电机调试窗口
- 首页参数区可直接设置“测量起点位置/测量终点位置”并写入 PLC
- 测量参数与测量运行流程共用独立的“PLC连接设置”窗口维护 IP/Port

### 2. 电机调试
- 通过 `IP + Port` 连接 PLC
- 支持只读参数轮询
- 支持整型、短整型、布尔型参数写入
- 参数地址、比例和单位统一定义在 `Definitions/MotorParameterDefinitions.cs`
- 写入映射逻辑集中在 `Services/Motor/MotorParameterSpecProvider.cs`

### 3. 点云导出与截面提取
- `PointCloudExportService` 用于枚举设备、设置 3D 点云模式并导出单帧点云
- 支持点云导出格式：`CSV`、`PLY`、`OBJ`
- 点云 SDK 图像模式集中定义在 `PointCloudExportService`：`Origin=1`、`PointCloud=4`、`Range=7`、`Intensity=10`
- 当前默认采集使用 `ImageMode=4`（3D 点云模式），直接 `GetImage` 获取点云数据，不再走深度图 `ImageMode=7` + `MapDepthToPointCloud` 转换
- `PointCloudRepresentativeProfileService` 支持从在线点云点集或 `CSV` 提取平均代表截面二维点集
- 代表点提取后会执行固定流程：离群点过滤 -> 外侧非工作面竖线旋转对齐 -> 标准边界 X 对齐 -> 工作面 Y 贴合
- `PointCloudMedianSectionCaptureService` 提供一条串联流程（在线优先）：
  `SDK 单帧采集 -> 内存点集提取平均代表截面`
- 在线链路异常时会自动回退：
  `SDK 单帧采集 -> 落盘 CSV -> 从 CSV 提取平均代表截面`

### 4. 点云在线采集参数
- 主界面“测量参数配置”区域支持配置“小车运行速度”和“单次测量总条数”
- 计算帧率按固定 1 cm 采样间距自动计算：`AcquisitionFrameRate = speedMetersPerMinute * 100 / 60`
- 配置会保存到运行目录 `point-cloud-capture-settings.json`，下次启动自动恢复
- 启动主界面测量流程前会自动校验并保存点云采集参数，避免遗漏手动保存
- 所有在线点云采集会在 `StartMeasure` 前写入：
  - `ImageMode = 4`
  - `AcquisitionFrameRate = speedMetersPerMinute * 100 / 60`
  - `Height = profileCount`
- 写入前会读取设备 `AcquisitionFrameRate` 和 `Height` 支持范围；超出范围或不满足 `Height` 步进时直接报错，不自动回退到深度图模式
- “点云导出”窗口的手动导出流程不读取主界面点云采集参数，只负责手动选择设备和格式导出

### 5. 打磨深度计算
- `RailSurfaceService.RailSurfaceFun(double x)` 提供标准轨面函数
- `RailSurfaceService.CalculateGrindDepths(IReadOnlyList<int> angles)` 支持多角度批量计算
- 批量计算时只采集一次代表截面点集，避免重复调用点云设备
- “计算需要打磨深度”时会同步保存当前各角度的代表廓形 `b` 值作为检测基线
- “检测已打磨深度”时会重新采集代表截面，并将当前 `b` 与基线 `b` 逐角度做差
- 检测基线默认保存到运行目录下的 `Log/grind-depth-baseline.json`
- “打磨深度调试”窗口支持导出最近一次计算使用的代表点坐标（CSV）

## 环境要求
### 软件要求
- Windows 系统
- `.NET 6 SDK`
- 可用的 `net6.0-windows` 开发环境

### 硬件与运行依赖
- 如需使用 PLC 调试能力，需要可访问的 `Modbus TCP` 设备
- 如需使用点云采集能力，需要本机可加载 `GrindCar/lib/Mv3dLpNet.dll`，并连接兼容的点云设备
- 若仅查看界面或进行部分离线逻辑调试，可在无 PLC、无点云设备的情况下运行

### SDK 版本约束
仓库根目录 `global.json` 指定：

```json
{
  "sdk": {
    "version": "6.0.0",
    "rollForward": "latestMinor",
    "allowPrerelease": false
  }
}
```

建议安装兼容的 .NET 6 SDK 后再执行构建。

## 安装与运行
在仓库根目录执行以下命令。

还原依赖：

```powershell
dotnet restore GrindCar.sln
```

构建项目：

```powershell
dotnet build GrindCar.sln
```

启动应用：

```powershell
dotnet run --project GrindCar/GrindCar.csproj
```

运行单元测试：

```powershell
dotnet test GrindCar.sln
```

清理构建产物：

```powershell
dotnet clean GrindCar.sln
```

## 使用说明
### 首页入口
1. 启动应用后进入“轨面打磨驾驶舱”首页。
2. 通过右上角按钮可打开对应功能窗口。
3. 当前可进入的窗口包括：`点云导出`、`打磨深度调试`、`点云算法验证`、`电机调试`。
4. 首页下方参数区可直接设置并写入：`测量起点位置`、`测量终点位置`、`打磨起点位置`、`打磨终点位置`。
5. 点击“设置PLC连接”可打开独立窗口维护测量参数写入用的 `IP/Port`。

### 电机调试流程
1. 打开“电机调试”窗口。
2. 输入 PLC 的 `IP` 和 `Port`。
3. 点击“连接”后开始轮询只读参数。
4. 对可写参数输入值后点击“修改”执行写入。
5. 点击“断开”后停止轮询并释放连接。

布尔输入当前支持以下格式：

- `true` / `false`
- `1` / `0`
- `on` / `off`
- `yes` / `no`
- `是` / `否`

### 点云调试流程
1. 在运行目录创建 `point-cloud-devices.json`，按点云设备序列号配置 `Left` / `Right` 侧别。
2. 打开“点云导出”窗口，选择设备并导出单帧点云文件。
3. 打开“打磨深度调试”窗口，输入一个或多个角度后点击“计算需要打磨深度”。
4. 系统在完成需要打磨深度计算后，会自动保存当前各角度的 `b` 值作为检测基线。
5. 机器完成打磨后，点击“检测已打磨深度”以重新采集点云并输出各角度的已打磨深度。

点云设备侧别配置示例：

```json
{
  "PointCloudDevices": [
    {
      "SerialNumber": "DEVICE_SN_LEFT",
      "Side": "Left"
    },
    {
      "SerialNumber": "DEVICE_SN_RIGHT",
      "Side": "Right"
    }
  ]
}
```

`Left` 设备的非工作面会对齐标准轨面 `x=-35.4` 临界点；`Right` 设备会对齐 `x=35.4` 临界点。枚举到未配置侧别的设备时，系统会直接报错。

### 主界面参数设置流程
1. 在首页参数区点击“设置PLC连接”，配置写入测量参数使用的 PLC `IP/Port`。
2. 在首页参数区输入“测量起点位置(m)”和“测量终点位置(m)”。
3. 点击“写入测量起止点”，系统将工程量按 `/100000` 比例换算后写入 PLC。
4. 测量参数地址：`D1140`（测量起点位置）、`D1142`（测量终点位置）。
5. 打磨参数地址：`D1180`（打磨起点位置）、`D1182`（打磨终点位置）。
6. 输入“小车运行速度(m/min)”和“单次测量总条数”，确认只读显示的“计算帧率(Hz)”。
7. 点击“保存点云采集参数”会保存到运行目录 `point-cloud-capture-settings.json`。
8. 点击“测量运动启动”后，系统会再次校验并保存点云采集参数，然后进入测量运行流程并在结束后写回打磨次数。

### 主界面测量运行流程
1. 点击“测量运动启动”，系统写入 `M31=1` 并开始轮询测量状态位。
2. 轮询期间监听 `M60`（启动当前廓形测量），按上升沿触发一次廓形仪采集。
3. 每次 `M60` 上升沿触发后，系统加载点云采集参数，按采集参数设置点云设备，采集单台廓形仪点云并计算平均代表截面。
4. 当前测量流程要求配置 2 台廓形仪；两台廓形仪按配置顺序分别采集，每采完一台写入 `M62=1` 表示当前廓形测量完成，`M62` 由 PLC 负责清零。
5. 两台廓形仪都完成后，系统合并代表截面点并计算一组各角度打磨深度；每组结果只缓存，不立即写打磨次数结果区。
6. 当读取到 `M61=1`（测量定位完成/测量运行结束）后，系统对已累计的各组测量结果做平均打磨深度计算。
7. 打磨次数计算公式：`打磨次数 = ceil(平均打磨深度 / 0.05)`。
8. 用户在确认窗口确认打磨深度后，最终将打磨次数写入 `D1800 + (N-1) * 2`（`N=1..19`，对应 19 个角度）。

## 关键模块说明
- `GrindCar/Views/MainWindow.xaml`：驾驶舱首页
- `GrindCar/Views/MotorDebugWindow.xaml`：电机调试窗口
- `GrindCar/Views/PointCloudExportWindow.xaml`：点云导出窗口
- `GrindCar/Views/GrindDepthDebugWindow.xaml`：打磨深度调试窗口
- `GrindCar/Views/PlcConnectionSettingsWindow.xaml`：PLC连接设置窗口（供首页参数区写入测量参数使用）
- `GrindCar/ViewModels/MotorViewModel.cs`：PLC 连接、轮询、写入、首页演示数据和状态文本管理
- `GrindCar/ViewModels/MainWindowMeasurementViewModel.cs`：主界面测量参数区状态与操作编排
- `GrindCar/ViewModels/GrindDepthDebugViewModel.cs`：打磨深度调试窗口状态与命令编排
- `GrindCar/ViewModels/PointCloudExportViewModel.cs`：点云导出窗口设备刷新、导出状态与流程编排
- `GrindCar/ViewModels/RepresentativeProfileComparisonViewModel.cs`：代表轨面与标准轨面对比窗口状态与绘图数据编排
- `GrindCar/Services/Motor/MotorParameterSpecProvider.cs`：电机读写参数映射与比例定义提供者
- `GrindCar/Services/Motor/PlcConnectionSettingsValidator.cs`：PLC 连接参数校验
- `GrindCar/Definitions/MotorParameterDefinitions.cs`：参数名称、地址、比例和单位定义中心
- `GrindCar/Services/PlcModbusCommunicator.cs`：Modbus 通信上下文（字段、地址常量与构造注入）
- `GrindCar/Services/PlcModbusCommunicator.Connection.cs`：连接管理与资源释放（Connect/Disconnect/Dispose）
- `GrindCar/Services/PlcModbusCommunicator.SingleReadWrite.cs`：单次读写与连接校验（Coil/Register 读写）
- `GrindCar/Services/PlcModbusCommunicator.Motion.cs`：运动编排与批量采集（MoveAndMonitor/GetData）
- `GrindCar/Services/PlcModbusCommunicator.ContinuousReading.cs`：持续读取任务与联动监控逻辑
- `GrindCar/Services/PlcModbusCommunicator.Conversion.cs`：寄存器与数值类型转换
- `GrindCar/Services/Measurement/MeasurementParameterService.cs`：主界面测量参数写入、测量运行轮询、平均打磨深度汇总与打磨次数写回
- `GrindCar/Services/Measurement/MeasurementGrindingWorkflowResult.cs`：测量运行流程结果汇总模型
- `GrindCar/Services/Measurement/MeasurementGrindingTimesResult.cs`：单角度平均深度与打磨次数模型
- `GrindCar/Services/Rail/Debug/GrindDepthDebugWorkflowService.cs`：打磨深度调试业务编排
- `GrindCar/Services/Rail/Debug/GrindDepthAngleParser.cs`：角度输入解析
- `GrindCar/Services/Rail/Debug/RepresentativePointsCsvExporter.cs`：代表点 CSV 导出
- `GrindCar/Services/Rail/Debug/RepresentativeProfileComparisonService.cs`：代表轨面与标准轨面对齐、采样与绘图数据计算
- `GrindCar/Services/PointCloud/PointCloudExportService.cs`：点云设备枚举、3D 点云模式采集、采集参数写入和文件导出
- `GrindCar/Models/PointCloud/PointCloudCaptureSettings.cs`：点云在线采集参数模型，包含小车速度(m/min)、单次测量总条数和按 1 cm 间距计算的帧率
- `GrindCar/Services/PointCloud/PointCloudCaptureSettingsStore.cs`：运行目录 `point-cloud-capture-settings.json` 的读写服务
- `GrindCar/Services/Rail/PointCloudRepresentativeProfileService.cs`：从在线点集或 CSV 提取平均代表截面二维点集
- `GrindCar/Services/Rail/Processing/PointCloudCsvReader.cs`：点云 CSV 解析（分隔符/表头/坐标列识别）
- `GrindCar/Services/Rail/Processing/RepresentativeProfilePointProcessor.cs`：离群过滤、旋转、对称扩展和平移
- `GrindCar/Services/Rail/Processing/QuickSelect.cs`：中位值快速选择算法
- `GrindCar/Services/Rail/PointCloudMedianSectionCaptureService.cs`：在线采集单帧点云并输出平均代表截面提取结果（失败自动回退 CSV，类型名保留 MedianSection）
- `GrindCar/Services/Rail/Core/StandardRailProfileSolver.cs`：标准轨面函数和切线 `b` 求解
- `GrindCar/Services/Rail/Core/RepresentativeSectionCaptureService.cs`：代表截面点采集
- `GrindCar/Services/Rail/Core/GrindingDepthBaselineStore.cs`：检测基线持久化
- `GrindCar/Services/RailSurfaceService.cs`：轨面计算外观层（Facade），对 UI 保持稳定调用入口

## 坐标与算法说明
### 点云坐标约定
- `X` 表示轨面横向
- `Y` 表示前进方向
- `Z` 表示高度

在 `RailProfilePoint` 中，当前语义为：

- `X`：轨面横向
- `Y`：高度 `Z`

### 平均代表截面定义
- 先从单帧点云中读取全部点，并过滤 `X/Y/Z` 全为 `0` 的异常点
- 按前进方向 `Y` 将点云拆成多条有效轮廓
- 每条轮廓按 `X` 排序，丢弃点数不足或 `X` 范围无效的轮廓
- 取所有有效轮廓 `X` 范围的公共交集
- 从有效轮廓相邻 `X` 差值中取中位数作为统一 X 网格步长
- 每条轮廓在统一 X 网格上对 `Z` 做线性插值
- 对同一网格 X 上的所有插值 `Z` 做算术平均，输出二维 `(X, AverageZ)` 点集
- `RepresentativeY` 表示参与平均代表截面的有效轮廓 `Y` 均值
- 当前实现会在平均代表截面生成后按固定流程处理：
- 先执行离群点过滤（局部拟合残差 + MAD 阈值）
- 按设备侧别固定选择外侧 10 个点作为非工作面竖线候选点
- 使用 PCA/TLS 拟合竖线，剔除离群候选点后重拟合
- 当竖线偏离垂直方向不超过 35° 时，自动尝试正反方向旋转，使竖线接近垂直
- 旋转后要求竖线残余倾角不超过 2°，且至少 85% 的点云主体位于正确侧
- 再将左侧边界对齐到 `x=-35.4`、右侧边界对齐到 `x=35.4`
- 最后按工作面点相对标准轨面的最小高度差完成 Y 方向贴合

### 打磨深度计算说明
- 输入角度以“度”为单位
- 斜率关系为 `k = tan(angle)`（`angle` 为角度制，内部按 `angle * π / 180` 转弧度）
- 结果基于标准轨面函数与采集到的代表截面点集计算得出
- 标准轨面函数整体下移 `176`，对应切线 `b` 与切点求解自动使用下移后的标准曲线

### 已打磨深度检测说明
- 系统会在“计算需要打磨深度”时记录当前各角度对应的代表廓形 `b` 值
- 检测阶段会重新采集当前代表截面，并重新计算各角度的 `b`
- 每个角度的已打磨深度计算公式为 `|b_current - b_baseline|`
- 检测必须依赖已保存的基线；若不存在基线，界面会提示先执行一次需要打磨深度计算

## 代码示例
### PLC 连接与写入
```csharp
using GrindCar.Services;

IPlcClient plc = new PlcModbusCommunicator("127.0.0.1", 502, 1);
await plc.ConnectAsync();
plc.WriteInt32(1000, 1500);
await plc.WriteSingleCoilAsync(104, true);
plc.Disconnect();
```

### 从 CSV 提取平均代表截面
```csharp
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

IPointCloudRepresentativeProfileService profileService = new PointCloudRepresentativeProfileService();

MedianSectionExtractionResult extractionResult =
    profileService.ExtractMedianSectionProfileFromCsv(@"D:\data\point-cloud.csv");

double representativeY = extractionResult.RepresentativeY;
IReadOnlyList<RailProfilePoint> profilePoints = extractionResult.ProfilePoints;
```

### 采集单帧点云并提取平均代表截面
```csharp
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

IPointCloudMedianSectionCaptureService captureService = new PointCloudMedianSectionCaptureService();

var settings = new PointCloudCaptureSettings(speedMetersPerMinute: 150.0, profileCount: 256);
PointCloudMedianSectionCaptureResult captureResult =
    captureService.CaptureMedianSectionProfile("DEVICE_SERIAL_NUMBER", PointCloudDeviceSide.Left, settings);

string csvPath = captureResult.CsvPath;
double representativeY = captureResult.ExtractionResult.RepresentativeY;
IReadOnlyList<RailProfilePoint> sectionPoints = captureResult.ExtractionResult.ProfilePoints;
```

### 计算多个打磨角度的打磨深度
```csharp
using GrindCar.Models.Rail;
using GrindCar.Services;

IReadOnlyList<GrindDepthResult> results =
    RailSurfaceService.GetGrindDepths(new[] { 0, 5, 10, 15 });
```

### 检测已打磨深度
```csharp
using GrindCar.Models.Rail;
using GrindCar.Services;

GrindingDepthBaseline baseline =
    RailSurfaceService.CreateGrindingDepthBaseline(new[] { 0, 5, 10, 15 });
RailSurfaceService.SaveGrindingDepthBaseline(baseline);

IReadOnlyList<DetectedGrindDepthResult> detectedResults =
    RailSurfaceService.DetectGrindingDepths(
        RailSurfaceService.LoadLatestGrindingDepthBaseline()!);
```

## API 示例
本项目不是 Web API 项目，当前没有 HTTP 接口。

如果从“对外调用方式”理解 API，则当前主要通过以下服务类对外提供能力：

- `IPlcClient`：PLC 连接、读取、写入
- `PointCloudExportService`：点云设备枚举、3D 点云模式采集、采集参数写入与文件导出
- `PointCloudCaptureSettingsStore`：点云在线采集参数持久化
- `IPointCloudRepresentativeProfileService`：从在线点集/CSV 提取平均代表截面
- `IPointCloudMedianSectionCaptureService`：采集点云并提取截面
- `RailSurfaceService`：标准轨面、打磨深度、代表廓形 `b` 值、检测基线生成/持久化及已打磨深度检测

## 构建与兼容性说明
- 当前命令行构建命令为 `dotnet build GrindCar.sln`
- 当前单元测试命令为 `dotnet test GrindCar.sln`
- `GrindCar.Tests` 已覆盖点云采集参数计算/持久化、点云设备参数范围校验以及平均代表截面插值提取
- 当前已知主要 NuGet 警告为 `NU1701`
- 该警告主要来自 `NModbus4 3.0.0-alpha1` 对 `net6.0-windows` 的兼容性声明不完整
- 如需进一步降低构建风险，优先评估替换或升级 Modbus 依赖

## 环境变量
当前项目不依赖环境变量。

## 注意事项
- 不要将 Modbus 地址、比例和单位散落到界面层或 code-behind 中
- 在线点云采集默认使用 `ImageMode=4` 的 3D 点云模式；`Range=7` 深度图模式仅保留常量，不作为默认采集路径
- 在线点云采集会按主界面保存的 `point-cloud-capture-settings.json` 写入 `AcquisitionFrameRate` 和 `Height`；配置不存在时会提示先设置并保存点云采集参数
- 点云采集默认走在线提取，在线失败时会在运行目录 `Log/` 目录落盘 `CSV` 后回退处理
- 代表点预处理逻辑会先离群点过滤，再按设备侧别识别外侧非工作面直线，并将非工作面顶部临界点平移对齐到标准轨面 `x=-35.4` 或 `x=35.4`
- 点云设备侧别配置文件为运行目录下的 `point-cloud-devices.json`，未配置设备不会参与默认推断，会直接报错
- 测量参数写入使用地址 `D1140/D1142`，比例 `/100000`，写入入口位于主界面参数区
- 打磨参数写入使用地址 `D1180/D1182`，比例 `/100000`，写入入口位于主界面参数区
- 主界面测量运行流程使用地址 `M31`（启动）、`M60`（当前廓形测量启动触发）、`M61`（测量定位完成/测量运行结束）、`M62`（当前廓形测量完成）
- 主界面支持打磨运动启动地址 `M32`（启动）
- 打磨次数结果写回区为 `D1800~D1836`（步长 2，对应 19 个固定测算角度，`D1800 + (N-1) * 2`）
- 打磨深度检测基线默认保存在 `Log/grind-depth-baseline.json`
- `bin/`、`obj/`、`tmp_obj/` 等构建产物不应提交到版本库
- 首页电量与速度当前为占位显示，不等同于实时设备遥测
