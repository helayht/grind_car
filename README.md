# GrindCar

## 项目简介
GrindCar 是一个基于 WPF 的轨面打磨设备上位机项目，当前包含多类主要界面：

- 驾驶舱首页：用于展示设备状态、速度和电量等监控信息
- 电机调试窗口：用于通过 Modbus TCP 与 PLC 进行参数读写、状态轮询和调试控制
- 点云与轨面调试窗口：用于点云导出、中位截面查看以及打磨深度计算调试

项目目前以桌面端本地运行方式为主，不依赖 Web 服务或环境变量配置。

## 技术栈
- .NET 6
- WPF
- NModbus4

## 工程结构
```text
GrindCar.sln
├─ GrindCar/
│  ├─ App.xaml
│  ├─ Definitions/      # 参数名称、地址、比例、单位定义
│  ├─ Infrastructure/   # 基础设施，例如 RelayCommand
│  ├─ Models/           # 数据模型
│  │  └─ Rail/          # 轨面截面点与点云处理结果模型
│  ├─ Services/         # PLC/Modbus 通信与业务服务
│  │  └─ Rail/          # 轨面点云处理服务
│  ├─ ViewModels/       # 视图模型
│  ├─ Views/            # WPF 窗口与界面
│  └─ Doc/              # 项目文档
```

## 安装说明
1. 安装 .NET 6 SDK。
2. 确认仓库根目录 [global.json](D:\工作\轨面打磨\GrindCar\global.json) 可解析到本机已安装的 .NET 6 SDK。
3. 在仓库根目录执行依赖还原：

```powershell
dotnet restore GrindCar.sln
```

## 运行说明
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
1. 启动后默认进入驾驶舱首页。
2. 点击首页右上角“电机调试”按钮，打开参数调试窗口。
3. 点击首页右上角“点云导出”“中位截面调试”或“打磨深度调试”按钮，可进入对应的点云与轨面调试界面。
4. 在电机调试窗口顶部输入 PLC 的 `IP` 和 `Port`。
5. 点击“连接”后开始轮询读取只读参数。
6. 对可写参数输入数值或布尔值后点击“修改”写入 PLC。
7. 在打磨深度调试窗口中输入一个或多个打磨角度，点击“开始计算”后查看每个角度对应的打磨深度。
8. 点击“断开”停止轮询并释放连接。

## 核心模块说明
- `Views/MainWindow.xaml`：驾驶舱首页
- `Views/MotorDebugWindow.xaml`：电机调试窗口
- `Views/PointCloudExportWindow.xaml`：点云导出窗口
- `Views/MedianSectionDebugWindow.xaml`：中位截面调试窗口
- `Views/GrindDepthDebugWindow.xaml`：打磨深度调试窗口
- `ViewModels/MotorViewModel.cs`：连接、轮询、写入和状态展示的主要逻辑
- `Services/PlcModbusCommunicator.cs`：Modbus TCP 通信封装
- `Services/PointCloud/PointCloudExportService.cs`：点云设备枚举、单帧采集与文件导出
- `Definitions/MotorParameterDefinitions.cs`：参数地址、比例和单位定义中心
- `Services/RailSurfaceService.cs`：轨面廓形相关计算逻辑
- `Models/Rail/RailProfilePoint.cs`：轨面截面二维点模型
- `Models/Rail/GrindDepthResult.cs`：打磨角度与打磨深度结果模型
- `Services/Rail/PointCloudRepresentativeProfileService.cs`：从点云 CSV 提取中位截面与代表廓形二维点集
- `Services/Rail/PointCloudMedianSectionCaptureService.cs`：从 SDK 采集单帧点云、落盘 `Log/CSV`、并提取中位 Y 截面二维点集

## 打磨深度计算
- `RailSurfaceService.GetGrindDepth(int angle)`：计算单个打磨角度对应的打磨深度。
- `RailSurfaceService.GetGrindDepths(IReadOnlyList<int> angles)`：批量计算多个打磨角度，并返回 `(Angle, GrindDepth)` 结果集合。
- 输入角度按“角度”语义处理，内部会先转换为弧度后再参与斜率计算。
- 批量计算时只采集一次代表截面点集，再复用该点集完成全部角度的深度计算。

## 点云代表廓形
- 当前点云处理链路采用离线验证方式：先由 `PointCloudExportService` 导出 `CSV`，再由 `PointCloudRepresentativeProfileService` 读取并提取代表廓形。
- 当前新增一条单帧直连链路：`SDK 单帧点云 -> Log 目录 CSV -> 中位 Y 截面二维点集 (X, Z)`。
- 当前坐标约定为：`Y` 表示前进方向，`X` 表示轨面横向，`Z` 表示高度。
- 当前“代表截面”定义为：对单帧点云全部 `Y` 去重后，按偏左中位规则选出中位 `Y`，再从原始点集中筛出该 `Y` 上的全部点，输出 `(X, Z)`。
- `RailProfilePoint` 在该流程中承载的是 `(横向 X, 高度 Z)` 二维坐标。
- 第一版默认参数：
  - `GridStepY = 0.2 mm`
- 当前实现会在读取 `CSV` 时直接完成分箱聚合，不再把整份点云加载为中间三维点列表。
- 单帧采集链路默认将原始点云 `CSV` 落到运行目录下的 `Log/` 目录，文件名格式为 `point-cloud-yyyyMMdd-HHmmss-fff.csv`。

## 代码示例
以下示例演示如何通过 `IPlcClient` 建立连接并执行基本写入：

```csharp
using GrindCar.Services;

IPlcClient plc = new PlcModbusCommunicator("127.0.0.1", 502, 1);
await plc.ConnectAsync();
plc.WriteInt32(1000, 1500);
await plc.WriteSingleCoilAsync(104, true);
plc.Disconnect();
```

以下示例演示如何从点云 `CSV` 提取代表廓形二维点集：

```csharp
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

IPointCloudRepresentativeProfileService profileService = new PointCloudRepresentativeProfileService();

IReadOnlyList<RailProfilePoint> profilePoints =
    profileService.ExtractRepresentativeProfile(@"D:\data\point-cloud.csv");
```

以下示例演示如何从设备采集单帧点云，自动写入 `Log/CSV`，并提取中位 `Y` 截面的二维点集：

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

以下示例演示如何批量计算多个打磨角度对应的打磨深度：

```csharp
using GrindCar.Models.Rail;
using GrindCar.Services;

IReadOnlyList<GrindDepthResult> results =
    RailSurfaceService.GetGrindDepths(new[] { 0, 5, 10, 15 });
```

## 当前实现说明
- 代表廓形提取当前聚焦于算法验证阶段，先支持点云 `CSV` 输入，不直接解析 SDK 点云内存。
- 当前已提供一个工程化折中方案：从 SDK 获取单帧点云后先导出为 `Log/CSV`，再复用现有 `CSV` 提取逻辑获取中位截面点集。
- 当前单帧直连链路只负责“拿到点云数据并选出中位 Y 截面代表点”，尚未接入 UI 自动触发。
- 当前已提供独立的打磨深度调试窗口，支持多角度输入并展示对应的打磨深度结果。
- 当前未将标准轨面对齐、残差分析、稳定性指标纳入代表廓形提取服务。

## 构建说明
- 当前命令行构建已验证通过：`dotnet build GrindCar.sln`
- 当前剩余主要构建警告为 `NU1701`
- 原因是 `NModbus4 3.0.0-alpha1` 不是针对 `net6.0-windows` 原生发布的包
- 如需继续清理警告，优先评估替换 `NModbus4`

## 环境变量
当前项目不依赖环境变量。
