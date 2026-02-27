# GrindCar

## 项目简介
GrindCar 是一套基于 WPF 的电机调试界面，用于通过 Modbus TCP 与 PLC 进行参数读写、状态轮询与操作控制。

## 技术栈
- .NET 6
- WPF
- NModbus4（Modbus TCP 通信）

## 安装说明
1. 安装 .NET 6 SDK。
2. 还原依赖：

```powershell
dotnet restore GrindCar.sln
```

## 运行说明
```powershell
dotnet run --project GrindCar/GrindCar.csproj
```

## 使用说明
1. 在界面顶部输入 PLC 的 `IP` 与 `Port`。
2. 点击“连接”开始轮询读取只读参数。
3. 对可写参数输入数值或布尔值并点击“修改”写入 PLC。
4. 点击“断开”停止轮询并释放连接。

## API 示例
以下示例展示了在业务层通过 `IPlcClient` 进行连接与写入的最小用法：

```csharp
using GrindCar.Services;

IPlcClient plc = new PlcModbusCommunicator("127.0.0.1", 502, 1);
await plc.ConnectAsync();
plc.WriteInt32(1000, 1500);
await plc.WriteSingleCoilAsync(104, true);
plc.Disconnect();
```

## 环境变量
当前项目不依赖环境变量配置。
