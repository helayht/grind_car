# GrindCar

## 项目简介
GrindCar 是一个基于 WPF 的轨面打磨设备上位机项目，当前包含两类主要界面：

- 驾驶舱首页：用于展示设备状态、速度和电量等监控信息
- 电机调试窗口：用于通过 Modbus TCP 与 PLC 进行参数读写、状态轮询和调试控制

项目目前以桌面端本地运行方式为主，不依赖 Web 服务或环境变量配置。

## 技术栈
- .NET 6
- WPF
- NModbus4
- 轨面拟合：分段平滑样条

## 工程结构
```text
GrindCar.sln
├─ GrindCar/
│  ├─ App.xaml
│  ├─ Definitions/      # 参数名称、地址、比例、单位定义
│  ├─ Infrastructure/   # 基础设施，例如 RelayCommand
│  ├─ Models/           # 数据模型
│  │  └─ Rail/          # 轨面截面点与拟合结果模型
│  ├─ Services/         # PLC/Modbus 通信与业务服务
│  │  └─ Rail/          # 轨面拟合服务
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
3. 在调试窗口顶部输入 PLC 的 `IP` 和 `Port`。
4. 点击“连接”后开始轮询读取只读参数。
5. 对可写参数输入数值或布尔值后点击“修改”写入 PLC。
6. 点击“断开”停止轮询并释放连接。

## 核心模块说明
- `Views/MainWindow.xaml`：驾驶舱首页
- `Views/MotorDebugWindow.xaml`：电机调试窗口
- `ViewModels/MotorViewModel.cs`：连接、轮询、写入和状态展示的主要逻辑
- `Services/PlcModbusCommunicator.cs`：Modbus TCP 通信封装
- `Definitions/MotorParameterDefinitions.cs`：参数地址、比例和单位定义中心
- `Services/RailSurfaceService.cs`：轨面廓形相关计算逻辑
- `Models/Rail/RailProfilePoint.cs`：轨面截面二维点模型
- `Models/Rail/RailProfileFitResult.cs`：拟合函数与有效定义域结果模型
- `Services/Rail/RailProfileFittingService.cs`：分段平滑样条拟合服务

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

以下示例演示如何对一组二维截面点执行轨面拟合：

```csharp
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;

var points = new[]
{
    new RailProfilePoint(-30.0, 165.2),
    new RailProfilePoint(-20.0, 171.8),
    new RailProfilePoint(-10.0, 175.4),
    new RailProfilePoint(0.0, 176.1),
    new RailProfilePoint(10.0, 175.0),
    new RailProfilePoint(20.0, 171.5),
    new RailProfilePoint(30.0, 164.7)
};

IRailProfileFittingService fittingService = new RailProfileFittingService();
RailProfileFitResult fitResult = fittingService.Fit(points);

double y = fitResult.Evaluate(5.0);
```

## 当前实现说明
- 轨面拟合当前为纯计算能力，尚未接入 UI。
- 拟合输入为一组可按 `x` 排序的二维散点。
- 拟合输出为函数 `f(x)` 与有效定义域 `[MinX, MaxX]`。
- 不做外推，超出定义域调用会抛出异常。

## 构建说明
- 当前命令行构建已验证通过：`dotnet build GrindCar.sln`
- 当前剩余主要构建警告为 `NU1701`
- 原因是 `NModbus4 3.0.0-alpha1` 不是针对 `net6.0-windows` 原生发布的包
- 如需继续清理警告，优先评估替换 `NModbus4`

## 环境变量
当前项目不依赖环境变量。
