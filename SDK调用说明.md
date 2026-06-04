# Mv3dLp SDK 调用说明

## 1. 文档目的

本文档用于说明当前项目中如何调用 `Mv3dLpNet.dll` 获取深度图、转换点云、显示和导出数据。

本文档基于当前项目源码与本地 SDK XML 注释整理，重点说明：

- 设备连接流程
- 采集流程
- 点云获取流程
- 当前项目中已经验证的调用方式
- 需要额外确认的点云内存解析问题

## 2. 当前项目中的调用主流程

当前项目获取点云的主流程如下：

```text
枚举设备
-> 打开设备
-> 设置 ImageMode = Range_Image
-> 开始测量
-> 循环获取深度图
-> 将深度图转换为点云
-> 显示点云或导出 CSV/PLY/OBJ
-> 停止测量
-> 关闭设备
```

## 3. 主要接口及作用

### 3.1 设备枚举

使用以下接口获取设备数量和设备列表：

```csharp
MV3D_LP_GetDeviceNumber(ref m_nDevNum);
MV3D_LP_GetDeviceList(m_stVector[0], m_nDevNum, ref m_nDevNum);
```

当前项目对应代码位置：

- [BasicDemo.cs#L88](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L88)

作用：

- 获取在线设备数量
- 获取每台设备的序列号、型号、IP 等信息

### 3.2 打开设备

使用序列号打开设备：

```csharp
MV3D_LP_OpenDeviceBySN(ref m_DevHandle, serialNumber);
```

当前项目对应代码位置：

- [BasicDemo.cs#L151](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L151)

作用：

- 根据设备序列号获取设备句柄
- 后续所有采集接口都依赖该句柄

### 3.3 设置采集图像模式

当前项目在打开设备后，将图像模式设置为深度图：

```csharp
MV3D_LP_PARAM pstValue = new MV3D_LP_PARAM();
MV3D_LP_ENUMPARAM enumParam = new MV3D_LP_ENUMPARAM();
enumParam.nCurValue = (uint)Mv3dLpImageMode.MV3D_LP_Range_Image;
pstValue.set_enumparam(enumParam);
MV3D_LP_SetParam(m_DevHandle, "ImageMode", pstValue);
```

当前项目对应代码位置：

- [BasicDemo.cs#L166](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L166)

说明：

- `MV3D_LP_Range_Image` 表示深度图模式
- 后续调用 `MV3D_LP_MapDepthToPointCloud(...)` 时，输入的就是深度图数据

### 3.4 开始测量

开始采集前调用：

```csharp
MV3D_LP_StartMeasure(m_DevHandle);
```

当前项目对应代码位置：

- [BasicDemo.cs#L300](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L300)

作用：

- 通知设备开始输出数据

### 3.5 获取图像数据

当前项目在接收线程中循环调用：

```csharp
MV3D_LP_IMAGE_DATA stImageData = new MV3D_LP_IMAGE_DATA();
UInt32 nTimeOut = 50;
int nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_DevHandle, stImageData, nTimeOut);
```

当前项目对应代码位置：

- [BasicDemo.cs#L277](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L277)

作用：

- 从设备取一帧图像数据
- 在当前配置下，这里取到的是深度图数据

### 3.6 深度图转点云

在取到深度图之后，调用：

```csharp
MV3D_LP_IMAGE_DATA stPointCloudImage = new MV3D_LP_IMAGE_DATA();
int nRet = Mv3dLpSDK.MV3D_LP_MapDepthToPointCloud(stImageData, stPointCloudImage);
```

当前项目对应代码位置：

- [BasicDemo.cs#L283](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L283)

作用：

- 将深度图转换为点云图像数据
- 输出仍然封装在 `MV3D_LP_IMAGE_DATA` 结构中

### 3.7 点云显示

当前项目直接调用显示接口显示点云：

```csharp
MV3D_LP_DisplayImage(pstImage, hWnd, Mv3dLpSDK.DisplayType_Auto, 0, 0);
```

当前项目对应代码位置：

- [BasicDemo.cs#L260](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L260)

说明：

- 当前项目将点云图像直接交给 SDK 显示
- 并没有手工解析 `pData` 内存中的点坐标

### 3.8 点云导出

当前项目通过统一保存接口将当前数据导出为 `CSV`、`PLY`、`OBJ`：

```csharp
MV3D_LP_SaveImage(m_stImageInfo, (uint)nFileType, strFileName);
```

当前项目对应代码位置：

- [BasicDemo.cs#L389](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L389)

说明：

- 如果 `m_stImageInfo` 中保存的是点云数据，则可导出点云文件
- 当前项目已提供按钮导出：
  - `CSV`
  - `PLY`
  - `OBJ`

### 3.9 停止测量

```csharp
MV3D_LP_StopMeasure(m_DevHandle);
```

当前项目对应代码位置：

- [BasicDemo.cs#L219](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L219)
- [BasicDemo.cs#L324](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L324)

### 3.10 关闭设备

```csharp
MV3D_LP_CloseDevice(ref m_DevHandle);
```

当前项目对应代码位置：

- [BasicDemo.cs#L220](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs#L220)

## 4. 推荐的标准调用顺序

推荐采用以下顺序调用 SDK：

### 4.1 初始化与连接

```csharp
UInt32 deviceCount = 0;
int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref deviceCount);

MV3D_LP_DEVICE_INFO_VECTOR deviceVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)deviceCount);
for (UInt32 i = 0; i < deviceCount; i++)
{
    deviceVector.Add(new MV3D_LP_DEVICE_INFO());
}

nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(deviceVector[0], deviceCount, ref deviceCount);

IntPtr devHandle = IntPtr.Zero;
nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref devHandle, deviceVector[0].chSerialNumber);
```

### 4.2 设置图像模式

```csharp
MV3D_LP_PARAM imageModeParam = new MV3D_LP_PARAM();
MV3D_LP_ENUMPARAM imageModeValue = new MV3D_LP_ENUMPARAM();
imageModeValue.nCurValue = (uint)Mv3dLpImageMode.MV3D_LP_Range_Image;
imageModeParam.set_enumparam(imageModeValue);

nRet = Mv3dLpSDK.MV3D_LP_SetParam(devHandle, "ImageMode", imageModeParam);
```

### 4.3 开始测量

```csharp
nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(devHandle);
```

### 4.4 获取深度图并转换点云

```csharp
MV3D_LP_IMAGE_DATA depthImage = new MV3D_LP_IMAGE_DATA();
MV3D_LP_IMAGE_DATA pointCloudImage = new MV3D_LP_IMAGE_DATA();

nRet = Mv3dLpSDK.MV3D_LP_GetImage(devHandle, depthImage, 50);
if (nRet == Mv3dLpSDK.MV3D_LP_OK)
{
    nRet = Mv3dLpSDK.MV3D_LP_MapDepthToPointCloud(depthImage, pointCloudImage);
}
```

### 4.5 保存点云

```csharp
if (nRet == Mv3dLpSDK.MV3D_LP_OK)
{
    nRet = Mv3dLpSDK.MV3D_LP_SaveImage(pointCloudImage, Mv3dLpSDK.FileType_CSV, "PointCloud");
}
```

### 4.6 结束采集

```csharp
Mv3dLpSDK.MV3D_LP_StopMeasure(devHandle);
Mv3dLpSDK.MV3D_LP_CloseDevice(ref devHandle);
```

## 5. 最简调用示例

以下代码只演示调用顺序，不包含完整异常处理和 UI 逻辑。

```csharp
UInt32 deviceCount = 0;
IntPtr devHandle = IntPtr.Zero;

int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref deviceCount);
if (nRet != Mv3dLpSDK.MV3D_LP_OK || deviceCount == 0)
{
    return;
}

MV3D_LP_DEVICE_INFO_VECTOR deviceVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)deviceCount);
for (UInt32 i = 0; i < deviceCount; i++)
{
    deviceVector.Add(new MV3D_LP_DEVICE_INFO());
}

nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(deviceVector[0], deviceCount, ref deviceCount);
if (nRet != Mv3dLpSDK.MV3D_LP_OK)
{
    return;
}

nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref devHandle, deviceVector[0].chSerialNumber);
if (nRet != Mv3dLpSDK.MV3D_LP_OK)
{
    return;
}

MV3D_LP_PARAM imageModeParam = new MV3D_LP_PARAM();
MV3D_LP_ENUMPARAM imageModeValue = new MV3D_LP_ENUMPARAM();
imageModeValue.nCurValue = (uint)4;
imageModeParam.set_enumparam(imageModeValue);
nRet = Mv3dLpSDK.MV3D_LP_SetParam(devHandle, "ImageMode", imageModeParam);
if (nRet != Mv3dLpSDK.MV3D_LP_OK)
{
    Mv3dLpSDK.MV3D_LP_CloseDevice(ref devHandle);
    return;
}

nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(devHandle);
if (nRet != Mv3dLpSDK.MV3D_LP_OK)
{
    Mv3dLpSDK.MV3D_LP_CloseDevice(ref devHandle);
    return;
}

MV3D_LP_IMAGE_DATA depthImage = new MV3D_LP_IMAGE_DATA();
MV3D_LP_IMAGE_DATA pointCloudImage = new MV3D_LP_IMAGE_DATA();

nRet = Mv3dLpSDK.MV3D_LP_GetImage(devHandle, depthImage, 50);
if (nRet == Mv3dLpSDK.MV3D_LP_OK)
{
    nRet = Mv3dLpSDK.MV3D_LP_MapDepthToPointCloud(depthImage, pointCloudImage);
    if (nRet == Mv3dLpSDK.MV3D_LP_OK)
    {
        Mv3dLpSDK.MV3D_LP_SaveImage(pointCloudImage, Mv3dLpSDK.FileType_CSV, "PointCloud");
    }
}

Mv3dLpSDK.MV3D_LP_StopMeasure(devHandle);
Mv3dLpSDK.MV3D_LP_CloseDevice(ref devHandle);
```

## 6. 关于点云数据的两种使用方式

### 6.1 方式一：直接导出 CSV 再解析

这是当前最稳妥的方式。

做法：

1. 获取深度图
2. 调用 `MV3D_LP_MapDepthToPointCloud(...)`
3. 调用 `MV3D_LP_SaveImage(..., FileType_CSV, ...)`
4. 从导出的 CSV 中读取 `X/Y/Z`

优点：

- 不需要自己解析 SDK 返回的点云内存布局
- 易于验证数据格式
- 适合先做算法验证

适合场景：

- 代表廓形计算
- 离线分析
- 算法原型开发

### 6.2 方式二：直接解析点云内存

`MV3D_LP_MapDepthToPointCloud(...)` 的输出仍为 `MV3D_LP_IMAGE_DATA`，实际点云坐标保存在其 `pData` 指针指向的内存中。

但当前项目源码没有给出以下关键信息：

- 点云单点结构体格式
- 每个点的字节长度
- 坐标是 `float`、`double` 还是整数缩放值
- 是否包含强度、颜色或无效点标志

因此，当前项目虽然能够显示和导出点云，但并没有直接解析 `pData`。

如果要走这条路，必须先确认：

- `MV3D_LP_IMAGE_DATA` 结构定义
- 点云图像类型下 `pData` 的排列格式
- SDK 文档是否提供点云点结构体定义

## 7. 关于 Profile 接口

SDK 本地注释中还存在以下接口：

- `MV3D_LP_GetProfile`
- `MV3D_LP_GetBatchProfile`

说明 SDK 可能直接支持轮廓数据获取。

如果目标是轨面代表廓形，而不是完整三维点云建模，则可以优先考虑：

- 直接取 Profile 数据
- 或先导出点云 CSV，再做二维截面提取

## 8. 当前项目中已确认的事实

基于当前源码，可以确认以下内容：

1. 当前项目使用 `MV3D_LP_Range_Image` 作为输入数据模式
2. 通过 `MV3D_LP_GetImage(...)` 获取深度图
3. 通过 `MV3D_LP_MapDepthToPointCloud(...)` 将深度图转换为点云
4. 点云结果可直接交给 SDK 显示
5. 点云结果可直接导出为 `CSV`、`PLY`、`OBJ`

## 9. 当前尚需确认的内容

若后续要做“实时直接解析点云坐标”，还需要确认以下问题：

1. `MV3D_LP_IMAGE_DATA` 在点云模式下的 `pData` 内存布局
2. 单点数据结构是否为 `XYZ` 或 `XYZI`
3. 坐标单位是否为毫米
4. 无效点如何标识

## 10. 推荐建议

对于当前轨面代表廓形项目，建议分两个阶段：

### 第一阶段

优先采用：

- `GetImage`
- `MapDepthToPointCloud`
- `SaveImage(...CSV...)`

先导出点云 `CSV`，再读取并做代表廓形算法。

### 第二阶段

待确认点云内存格式后，再考虑：

- 在程序内部直接解析 `pointCloudImage.pData`
- 实时提取截面
- 实时计算代表廓形

## 11. 参考代码位置

- [BasicDemo.cs](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/BasicDemo.cs)
- [Mv3dLpNet.xml](/D:/工作/轨面打磨/BasicDemo_DepthToPointCloud/bin/AnyCpu/Debug/Mv3dLpNet.xml)

## 12. 结论

当前项目已经验证了使用 SDK 获取点云的基本方式，标准调用链路为：

```text
OpenDevice
-> SetParam(ImageMode = Range_Image)
-> StartMeasure
-> GetImage
-> MapDepthToPointCloud
-> DisplayImage 或 SaveImage
-> StopMeasure
-> CloseDevice
```

如果你当前要做轨面代表廓形，建议先通过 `CSV` 导出点云再处理，这是当前最稳妥、实现成本最低的方案。
