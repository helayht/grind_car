# GrindCar

## 项目简介
GrindCar 是一个基于 `.NET 6 + WPF` 的轨面打磨设备上位机项目，面向本地桌面场景，当前主要覆盖以下能力：

- 驾驶舱首页展示电量、速度、连接状态等运行信息
- 电机调试窗口通过 `Modbus TCP` 与 PLC 建立连接，完成参数轮询、写入和调试
- 点云调试链路支持设备枚举、单帧点云导出、CSV 中位截面提取、需要打磨深度计算与已打磨深度检测
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
│  │  ├─ PointCloud/      # 点云设备 SDK 封装与导出
│  │  └─ Rail/            # 中位截面与代表廓形提取
│  ├─ ViewModels/         # ViewModel
│  ├─ Views/              # WPF 窗口
│  ├─ Converters/         # XAML 转换器
│  ├─ lib/                # 第三方本地 DLL
│  └─ Doc/                # 项目文档
├─ global.json
└─ README.md
```

## 核心功能
### 1. 驾驶舱首页
- 启动后默认进入首页
- 展示电量、速度、连接状态和当前时间
- 首页中的电量和速度当前为演示数据，由 `MotorViewModel` 定时刷新
- 可从首页进入点云导出、中位截面调试、打磨深度调试和电机调试窗口

### 2. 电机调试
- 通过 `IP + Port` 连接 PLC
- 支持只读参数轮询
- 支持整型、短整型、布尔型参数写入
- 参数地址、比例和单位统一定义在 `Definitions/MotorParameterDefinitions.cs`
- 写入映射逻辑集中在 `ViewModels/MotorViewModel.BuildWriteSpecs()`

### 3. 点云导出与截面提取
- `PointCloudExportService` 用于枚举设备并导出单帧点云
- 支持点云导出格式：`CSV`、`PLY`、`OBJ`
- `PointCloudRepresentativeProfileService` 支持从点云 `CSV` 中提取中位 `Y` 截面二维点集
- `PointCloudMedianSectionCaptureService` 提供一条串联流程：
  `SDK 单帧采集 -> 落盘到 Log 目录 -> 从 CSV 提取中位截面`

### 4. 打磨深度计算
- `RailSurfaceService.RailSurfaceFun(double x)` 提供标准轨面函数
- `RailSurfaceService.GetGrindDepth(int angle)` 计算单个角度的打磨深度
- `RailSurfaceService.GetGrindDepths(IReadOnlyList<int> angles)` 支持多角度批量计算
- 批量计算时只采集一次代表截面点集，避免重复调用点云设备
- “计算需要打磨深度”时会同步保存当前各角度的代表廓形 `b` 值作为检测基线
- “检测已打磨深度”时会重新采集代表截面，并将当前 `b` 与基线 `b` 逐角度做差
- 检测基线默认保存到运行目录下的 `Log/grind-depth-baseline.json`

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

清理构建产物：

```powershell
dotnet clean GrindCar.sln
```

## 使用说明
### 首页入口
1. 启动应用后进入“轨面打磨驾驶舱”首页。
2. 通过右上角按钮可打开对应功能窗口。
3. 当前可进入的窗口包括：`点云导出`、`中位截面调试`、`打磨深度调试`、`电机调试`。

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
1. 打开“点云导出”窗口，选择设备并导出单帧点云文件。
2. 打开“中位截面调试”窗口，验证从 `CSV` 中提取的中位截面点集。
3. 打开“打磨深度调试”窗口，输入一个或多个角度后点击“计算需要打磨深度”。
4. 系统在完成需要打磨深度计算后，会自动保存当前各角度的 `b` 值作为检测基线。
5. 机器完成打磨后，点击“检测已打磨深度”以重新采集点云并输出各角度的已打磨深度。

## 关键模块说明
- `GrindCar/Views/MainWindow.xaml`：驾驶舱首页
- `GrindCar/Views/MotorDebugWindow.xaml`：电机调试窗口
- `GrindCar/Views/PointCloudExportWindow.xaml`：点云导出窗口
- `GrindCar/Views/MedianSectionDebugWindow.xaml`：中位截面调试窗口
- `GrindCar/Views/GrindDepthDebugWindow.xaml`：打磨深度调试窗口
- `GrindCar/ViewModels/MotorViewModel.cs`：PLC 连接、轮询、写入、首页演示数据和状态文本管理
- `GrindCar/Definitions/MotorParameterDefinitions.cs`：参数名称、地址、比例和单位定义中心
- `GrindCar/Services/PlcModbusCommunicator.cs`：Modbus TCP 通信封装
- `GrindCar/Services/PointCloud/PointCloudExportService.cs`：点云设备枚举、采集和文件导出
- `GrindCar/Services/Rail/PointCloudRepresentativeProfileService.cs`：从 CSV 提取中位截面二维点集
- `GrindCar/Services/Rail/PointCloudMedianSectionCaptureService.cs`：采集单帧点云并输出中位截面提取结果
- `GrindCar/Services/RailSurfaceService.cs`：标准轨面函数、打磨深度计算、代表廓形 `b` 值计算、检测基线生成/持久化及已打磨深度检测逻辑

## 坐标与算法说明
### 点云坐标约定
- `X` 表示轨面横向
- `Y` 表示前进方向
- `Z` 表示高度

在 `RailProfilePoint` 中，当前语义为：

- `X`：轨面横向
- `Y`：高度 `Z`

### 中位截面定义
- 先从单帧点云中读取全部有效点
- 对全部 `Y` 坐标去重
- 选择排序后偏左的中位 `Y`
- 筛出该 `Y` 对应的全部点
- 输出二维 `(X, Z)` 点集用于后续轨面计算

### 打磨深度计算说明
- 输入角度以“度”为单位
- 内部会先转为弧度，再换算斜率参与计算
- 结果基于标准轨面函数与采集到的代表截面点集计算得出

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

### 从 CSV 提取中位截面
```csharp
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

IPointCloudRepresentativeProfileService profileService = new PointCloudRepresentativeProfileService();

MedianSectionExtractionResult extractionResult =
    profileService.ExtractMedianSectionProfileFromCsv(@"D:\data\point-cloud.csv");

double medianY = extractionResult.MedianY;
IReadOnlyList<RailProfilePoint> profilePoints = extractionResult.ProfilePoints;
```

### 采集单帧点云并提取截面
```csharp
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

IPointCloudMedianSectionCaptureService captureService = new PointCloudMedianSectionCaptureService();

PointCloudMedianSectionCaptureResult captureResult =
    captureService.CaptureMedianSectionProfile("DEVICE_SERIAL_NUMBER");

string csvPath = captureResult.CsvPath;
double medianY = captureResult.ExtractionResult.MedianY;
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
- `PointCloudExportService`：点云设备枚举与文件导出
- `IPointCloudRepresentativeProfileService`：从 CSV 提取中位截面
- `IPointCloudMedianSectionCaptureService`：采集点云并提取截面
- `RailSurfaceService`：标准轨面、打磨深度、代表廓形 `b` 值、检测基线生成/持久化及已打磨深度检测

## 构建与兼容性说明
- 当前命令行构建命令为 `dotnet build GrindCar.sln`
- 当前已知主要 NuGet 警告为 `NU1701`
- 该警告主要来自 `NModbus4 3.0.0-alpha1` 对 `net6.0-windows` 的兼容性声明不完整
- 如需进一步降低构建风险，优先评估替换或升级 Modbus 依赖

## 环境变量
当前项目不依赖环境变量。

## 注意事项
- 不要将 Modbus 地址、比例和单位散落到界面层或 code-behind 中
- 点云采集默认会在运行目录下创建 `Log/` 目录并落盘 `CSV`
- 打磨深度检测基线默认保存在 `Log/grind-depth-baseline.json`
- `bin/`、`obj/`、`tmp_obj/` 等构建产物不应提交到版本库
- 首页当前部分监控数据为演示数据，不等同于实时设备遥测
