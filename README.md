# GrindCar

## 项目简介
GrindCar 是一个基于 `.NET 6 + WPF` 的轨面打磨设备上位机项目，面向本地桌面场景，当前主要覆盖以下能力：

- 驾驶舱首页展示电量、速度、连接状态等运行信息
- 电机调试窗口通过 `Modbus TCP` 与 PLC 建立连接，完成参数轮询、写入和调试
- 点云调试链路支持设备枚举、深度图转点云单帧导出、在线点云平均代表截面提取（失败时自动回退 CSV）和打磨深度算法验证
- 主界面内置参数设置，可写入“测量起点位置/测量终点位置”与“打磨起点位置/打磨终点位置”，并可配置点云在线采集速度、单次测量总条数和计算帧率；PLC 连接参数通过独立窗口维护
- 主界面支持测量运行流程：按 `M31/M60/M61/M62` 交互，两台廓形仪按顺序采集并合并为一组测量，累计多组后计算平均打磨深度并写回各角度打磨次数；并支持独立写入打磨运动启动 `M32`
- 轨面服务层提供标准轨面函数、代表截面提取和多角度打磨深度计算能力

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
│  │     ├─ Core/         # 轨面求解、配准、代表截面采集核心服务
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
- 可从首页进入点云导出、点云算法验证、代表廓形配准和电机调试窗口
- 首页参数区可直接设置“测量起点位置/测量终点位置”并写入 PLC
- 测量参数与测量运行流程共用独立的“PLC连接设置”窗口维护 IP/Port

### 2. 电机调试
- 通过 `IP + Port` 连接 PLC
- 支持只读参数轮询
- 支持整型、短整型、布尔型参数写入
- 参数地址、比例和单位统一定义在 `Definitions/MotorParameterDefinitions.cs`
- 写入映射逻辑集中在 `Services/Motor/MotorParameterSpecProvider.cs`

### 3. 点云导出与截面提取
- `PointCloudExportService` 用于枚举设备、设置 Range 深度图模式并导出单帧点云
- 支持点云导出格式：`CSV`、`PLY`、`OBJ`
- 点云 SDK 图像模式集中定义在 `PointCloudExportService`：`Origin=1`、`PointCloud=4`、`Range=7`、`Intensity=10`
- 当前默认采集使用 `ImageMode=7`（Range 深度图模式），先通过 `GetImage` 获取深度图，再调用 `MapDepthToPointCloud` 转换为点云
- `PointCloudRepresentativeProfileService` 支持从在线点云点集或 `CSV` 提取平均代表截面二维点集
- 同一次 Left/Right 点云分析会复用有效点过滤、方向变换和 Y 分组结果，再分别计算平均代表截面与最大掉块；CSV 路径每侧只解析一次文件
- 带 `Left` / `Right` 侧别的代表点提取会执行固定流程：原始有效点镜像 -> 绕原始点 X/Z 质心旋转 -> 平均代表截面提取与离群过滤 -> 平移和 X 范围裁切
- 手动配准参数保存在运行目录 `point-cloud-profile-registration.json`，当前算法版本为 `2`；旧版本配置必须重新完成配准并保存
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
  - `ImageMode = 7`
  - `AcquisitionFrameRate = speedMetersPerMinute * 100 / 60`
  - `LSLRangeImgHeight = profileCount`
- 写入前会读取设备 `AcquisitionFrameRate` 和 `LSLRangeImgHeight` 支持范围；超出范围或不满足 `LSLRangeImgHeight` 步进时直接报错
- “点云导出”窗口的手动导出流程不读取主界面点云采集参数，只负责手动选择设备和格式导出

### 5. 打磨深度计算
- `RailSurfaceService.RailSurfaceFun(double x)` 提供标准轨面函数
- `RailSurfaceService.CalculateGrindDepths(IReadOnlyList<int> angles)` 支持多角度批量计算
- 批量计算时只采集一次代表截面点集，避免重复调用点云设备
- “点云算法验证”窗口可基于 Left / Right 原始点云 CSV 对比常规深度、掉块深度和最终深度
- 主界面测量流程会汇总多组测量结果，并在用户确认后回写各角度打磨次数

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
3. 当前可进入的窗口包括：`点云导出`、`点云算法验证`、`代表廓形配准`、`电机调试`。
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
2. 打开“点云导出”窗口，选择设备并导出 Left / Right 原始点云 CSV。
3. 打开“代表廓形配准”窗口，分别选择 Left / Right CSV，通过 X/Y 平移、旋转、镜像选项以及 X 范围裁切将两侧代表廓形对齐到标准参考线。镜像和旋转会先作用于原始点，再重新生成平均代表廓形；自动精对齐直接对当前缓存的平均代表廓形执行一次稳健 ICP，合并参数后仅重建一次当前侧廓形，不会循环处理原始点。
4. 参数编辑后点击“确认调整”同步重新处理原始点并刷新预览，确认无误后点击“保存参数”，系统会将 Left / Right 配准参数和算法版本保存到当前轨型对应的配置文件。
5. 打开“点云算法验证”窗口，选择 Left / Right 原始点云 CSV 并输入角度。窗口会显示常规深度、掉块深度和两者的最终最大值，并可查看最大掉块廓形。

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

`Left` / `Right` 侧别用于选择对应的手动配准参数。枚举到未配置侧别的设备时，系统会直接报错；已配置侧别但未保存代表廓形配准参数时，系统会提示先完成手动配准。

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
- `GrindCar/Views/ProfileRegistrationWindow.xaml`：代表廓形手动配准窗口
- `GrindCar/Views/PlcConnectionSettingsWindow.xaml`：PLC连接设置窗口（供首页参数区写入测量参数使用）
- `GrindCar/ViewModels/MotorViewModel.cs`：PLC 连接、轮询、写入、首页演示数据和状态文本管理
- `GrindCar/ViewModels/MainWindowMeasurementViewModel.cs`：主界面测量参数区状态与操作编排
- `GrindCar/ViewModels/PointCloudExportViewModel.cs`：点云导出窗口设备刷新、导出状态与流程编排
- `GrindCar/ViewModels/ProfileRegistrationViewModel.cs`：代表廓形手动配准、误差反馈和参数保存
- `GrindCar/ViewModels/RepresentativeProfileComparisonViewModel.cs`：代表轨面与标准轨面对比窗口状态与绘图数据编排
- `GrindCar/Services/Motor/MotorParameterSpecProvider.cs`：电机读写参数映射与比例定义提供者
- `GrindCar/Services/Motor/PlcConnectionSettingsValidator.cs`：PLC 连接参数校验
- `GrindCar/Definitions/MotorParameterDefinitions.cs`：参数名称、地址、比例和单位定义中心
- `GrindCar/Services/PlcModbusCommunicator.cs`：Modbus 通信上下文（连接字段与构造注入）
- `GrindCar/Services/PlcModbusCommunicator.Connection.cs`：连接管理与资源释放（ConnectAsync/Disconnect/Dispose）
- `GrindCar/Services/PlcModbusCommunicator.SingleReadWrite.cs`：单次读写与连接校验（Coil/Register 读写）
- `GrindCar/Services/PlcModbusCommunicator.Conversion.cs`：整型寄存器与数值类型转换
- `GrindCar/Services/Measurement/MeasurementParameterService.cs`：主界面测量参数写入、测量运行轮询、平均打磨深度汇总与打磨次数写回
- `GrindCar/Services/Measurement/MeasurementGrindingWorkflowResult.cs`：测量运行流程结果汇总模型
- `GrindCar/Services/Measurement/MeasurementGrindingTimesResult.cs`：单角度平均深度与打磨次数模型
- `GrindCar/Services/Rail/Debug/GrindDepthAngleParser.cs`：角度输入解析
- `GrindCar/Services/Rail/Debug/RepresentativeProfileComparisonService.cs`：代表轨面与标准轨面对比、采样与绘图数据计算
- `GrindCar/Services/PointCloud/PointCloudExportService.cs`：点云设备枚举、Range 深度图采集、深度图转点云、采集参数写入和文件导出
- `GrindCar/Models/PointCloud/PointCloudCaptureSettings.cs`：点云在线采集参数模型，包含小车速度(m/min)、单次测量总条数和按 1 cm 间距计算的帧率
- `GrindCar/Services/PointCloud/PointCloudCaptureSettingsStore.cs`：运行目录 `point-cloud-capture-settings.json` 的读写服务
- `GrindCar/Services/Rail/PointCloudRepresentativeProfileService.cs`：从在线点集或 CSV 提取平均代表截面二维点集，并按侧别应用手动配准参数
- `GrindCar/Services/Rail/Core/ProfileRegistrationSettingsStore.cs`：代表廓形配准参数和算法版本存储，按轨型独立保存（`point-cloud-profile-registration-60kg.json` / `point-cloud-profile-registration-50kg.json`），旧算法版本会提示重新配准
- `GrindCar/Services/Rail/Core/ProfileRegistrationTransformService.cs`：原始点镜像/旋转、代表点平移/裁切、完整二维刚体变换、ICP 增量参数换算和标准轨面贴合误差计算
- `GrindCar/Services/Rail/Processing/PointCloudCsvReader.cs`：点云 CSV 解析（分隔符/表头/坐标列识别）
- `GrindCar/Services/Rail/Processing/RepresentativeProfilePointProcessor.cs`：离群过滤（自适应窗口）、旋转、对称扩展和平移
- `GrindCar/Services/Rail/Processing/QuickSelect.cs`：中位值快速选择算法
- `GrindCar/Services/Rail/PointCloudMedianSectionCaptureService.cs`：在线采集单帧点云并输出平均代表截面提取结果（失败自动回退 CSV，类型名保留 MedianSection）
- `GrindCar/Services/Rail/Core/StandardRailProfileSolver.cs`：标准轨面函数、切线 `b` 求解及基于前 0.5% 最大候选值平均的代表截距 `b` 求解（抗异常值）。支持 60kg/m 和 50kg/m 轨面型号动态切换（`SwitchProfile`），切换时自动清空切线求解缓存
- `GrindCar/Services/Rail/MaximumDropProfileService.cs`：按原始点云 Y 截面定位连续向下缺失，应用连续 3 点残差中位抗飞点，并保留滤波后廓形的原始高度
- `GrindCar/Services/Rail/Core/RepresentativeSectionCaptureService.cs`：代表截面点采集
- `GrindCar/Services/Rail/Core/RobustIcpRegistrationService.cs`：纯 C# 稳健 ICP（迭代最近点）二维精对齐服务。采用截断匹配策略（保留 80% 最佳拟合点对）以抵抗边缘噪点和局部离群；多轮迭代中按刚体变换矩阵规则复合旋转和平移，保证返回参数能准确复现内部对齐结果
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
- 带侧别提取时读取算法版本为 `2` 的配准参数；当 `IsMirrored=true` 时，Left 以原始有效点 `MaxX`、Right 以原始有效点 `MinX` 为轴执行 X 镜像
- 计算镜像后全部原始有效点的 X/Z 质心 `(cx, cz)`，仅旋转 X/Z：`x' = (x-cx)*cos(θ) - (z-cz)*sin(θ) + cx`，`z' = (x-cx)*sin(θ) + (z-cz)*cos(θ) + cz`；前进方向 Y 保持不变
- 按前进方向 `Y` 将点云拆成多条有效轮廓；分组容差为 `0.25 mm`，与廓形仪 Y 方向物理分辨率匹配，避免传感器微小噪声将同一物理截面拆成多个伪截面
- 每条轮廓按 `X` 排序，丢弃点数不足或 `X` 范围无效的轮廓；有效轮廓数 < 5 时拒绝计算（防止数据质量异常时静默产出不可靠结果）
- 取所有有效轮廓 `X` 范围的公共交集
- 从有效轮廓相邻 `X` 差值中先取每截面均值，再取截面间中位数作为统一 X 网格步长（两层统计抵御异常截面干扰）
- 每条轮廓在统一 X 网格上对 `Z` 做线性插值
- 对同一网格 X 上的所有插值 `Z` 做算术平均，输出二维 `(X, AverageZ)` 点集
- `RepresentativeY` 表示参与平均代表截面的有效轮廓 Y 的中位数（避免采样密度偏差导致代表 Y 偏移）
- 平均代表截面生成后执行局部拟合残差 + MAD 离群过滤，再应用 `Dx/Dy` 平移，最后按最终 X 坐标执行 `XMin/XMax` 裁切
- 配置文件不保存质心，每次从当前原始有效点重新计算；算法版本缺失或不为 `2` 时拒绝加载，必须重新打开“代表廓形配准”窗口校准并保存
- 配准窗口的自动精对齐以当前已确认参数生成的代表廓形为 ICP 输入，只调用一次 ICP 服务；ICP 内部最多执行 30 次收敛迭代，所得刚体增量合并到现有 `RotationDegrees/Dx/Dy` 后，仅重新生成当前侧代表廓形用于最终预览
- ICP 最近点匹配只处理压缩后的代表点和 241 个标准采样点，不直接遍历原始点；自动对齐的原始点处理成本由最多三轮重建降低为参数合并后的一次当前侧重建
- 若缺少手动配准参数，带侧别的代表廓形提取会直接报错，不再回退到旧的自动边界对齐

### 打磨深度计算说明
- 输入角度以”度”为单位
- 深度计算使用稳定的单位法向支撑值 `c = Z*cos(angle) - X*sin(angle)`，不再通过 `tan(angle)` 计算，因此 `90°` 不会发生数值发散
- 结果基于标准轨面函数与采集到的代表截面点集计算得出
- 标准轨面函数整体下移 `176`，标准与测量廓形的法向支撑值都使用下移后的坐标系
- **代表支撑值的抗异常值计算**：测量廓形取前 0.5% 最大候选 `c` 的算术平均；点数较少时退化为取单个最大值

### 最大掉块深度与最终打磨深度说明
- Left/Right 原始点云分别按前进方向 `Y` 拆成二维 `(X,Z)` 廓形，不进行纵向匹配，也不使用平均代表廓形定位掉块
- 每条廓形完成方向校正、`Dx/Dy` 平移和 X 裁切后，先根据该截面的实际 `[XMin,XMax]` 求当前 50/60 kg/m 标准轨面在此区间内的最低高度，再以低于该高度的点为硬分隔拆分有效片段；超出标准定义域的 X 不参与阈值计算，三维点云的前进方向 `Y` 不参与筛选
- 每个第一次筛选后的有效片段独立计算残差 `r = Z - RailSurfaceFun(X)`，并以连续 3 点残差中位值抑制单点飞点；三点窗口不会跨越被删除点形成的片段边界
- 根据过滤残差重构 `Z` 后，按重构点的新 X 范围重新求一次区间最低高度并执行第二次筛选；两次筛选形成的边界都会中断连续负残差计数，最大掉块窗口按片段分别绘制折线，不跨缺口连线
- 仅严格小于 `0` 的过滤后残差表示向下缺失；至少连续 3 个负残差点才构成有效掉块区域，完全位于标准轨面上方的廓形不会参与选择
- 单条廓形最大掉块深度为 `max(0, -min(r_valid))`；深度大于 `3 mm` 的廓形视为异常数据并排除，等于 `3 mm` 的廓形仍可参与选择
- 整次测量分别从有效数据中保留 Left/Right 最大掉块廓形，不对掉块深度取平均；某侧没有向下掉块时该侧深度按 `0` 处理，不中断常规深度计算
- 最大掉块廓形不再执行额外的整体高度平移，直接使用滤波后、已配准裁切的原高度点集计算单侧掉块打磨深度
- 掉块角度按侧别匹配：正角度只使用 Left，负角度只使用 Right，`0°` 比较两侧；不匹配侧按 `0` 处理，避免把未覆盖的半边误判成掉块
- 单侧掉块打磨深度为 `max(0, c_standard - c_side)`，其中 `c = Z*cos(angle) - X*sin(angle)`；只保留测量廓形低于标准轨面的法向缺失量
- 最大掉块对比窗口按实际点顺序绘制原始折线，不使用 Catmull–Rom 平滑；普通代表廓形窗口仍使用平滑曲线
- 常规深度仍使用现有平均代表廓形并在多组测量间取平均；最终结果为 `max(常规平均深度, Left 掉块深度, Right 掉块深度)`
- 打磨次数仍按 `ceil(最终深度 / 0.05)` 计算

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

GrindDepthCalculationResult calculation =
    RailSurfaceService.CalculateGrindDepths(new[] { 0, 5, 10, 15 });
IReadOnlyList<GrindDepthResult> results = calculation.Results;
```

## API 示例
本项目不是 Web API 项目，当前没有 HTTP 接口。

如果从“对外调用方式”理解 API，则当前主要通过以下服务类对外提供能力：

- `IPlcClient`：PLC 连接、读取、写入
- `PointCloudExportService`：点云设备枚举、Range 深度图采集、深度图转点云、采集参数写入与文件导出
- `PointCloudCaptureSettingsStore`：点云在线采集参数持久化
- `IPointCloudRepresentativeProfileService`：从在线点集/CSV 提取平均代表截面
- `IPointCloudMedianSectionCaptureService`：采集点云并提取截面
- `RailSurfaceService`：标准轨面、代表截面采集和打磨深度计算

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
- 在线点云采集默认使用 `ImageMode=7` 的 Range 深度图模式；采集后通过 `MapDepthToPointCloud` 转换为点云
- 在线点云采集会按主界面保存的 `point-cloud-capture-settings.json` 写入 `AcquisitionFrameRate` 和 `LSLRangeImgHeight`；配置不存在时会提示先设置并保存点云采集参数
- 点云采集默认走在线提取，在线失败时会在运行目录 `Log/` 目录落盘 `CSV` 后回退处理
- 带侧别的代表点预处理逻辑会先对原始有效点执行镜像和旋转，再提取平均代表截面并过滤，最后应用平移和 X 范围裁切
- 后续测量必须先完成代表廓形手动配准并保存参数；缺少 `point-cloud-profile-registration.json` 时不会执行自动对齐兜底
- 点云设备侧别配置文件为运行目录下的 `point-cloud-devices.json`，未配置设备不会参与默认推断，会直接报错
- 测量参数写入使用地址 `D1140/D1142`，比例 `/100000`，写入入口位于主界面参数区
- 打磨参数写入使用地址 `D1180/D1182`，比例 `/100000`，写入入口位于主界面参数区
- 主界面测量运行流程使用地址 `M31`（启动）、`M60`（当前廓形测量启动触发）、`M61`（测量定位完成/测量运行结束）、`M62`（当前廓形测量完成）
- 主界面支持打磨运动启动地址 `M32`（启动）
- 打磨次数结果写回区为 `D1800~D1836`（步长 2，对应 19 个固定测算角度，`D1800 + (N-1) * 2`）
- `bin/`、`obj/`、`tmp_obj/` 等构建产物不应提交到版本库
- 首页电量与速度当前为占位显示，不等同于实时设备遥测
