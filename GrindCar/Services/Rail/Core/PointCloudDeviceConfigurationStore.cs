using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 读取点云设备侧别配置。
/// </summary>
public sealed class PointCloudDeviceConfigurationStore
{
    private const string DefaultConfigurationFileName = "point-cloud-devices.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configurationFilePath;

    public PointCloudDeviceConfigurationStore(string? configurationFilePath = null)
    {
        _configurationFilePath = string.IsNullOrWhiteSpace(configurationFilePath)
            ? Path.Combine(Environment.CurrentDirectory, DefaultConfigurationFileName)
            : configurationFilePath;
    }

    public IReadOnlyDictionary<string, PointCloudDeviceSide> LoadDeviceSideMap()
    {
        if (!File.Exists(_configurationFilePath))
        {
            throw new InvalidOperationException($"点云设备配置文件不存在: {_configurationFilePath}");
        }

        string json = File.ReadAllText(_configurationFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"点云设备配置文件为空: {_configurationFilePath}");
        }

        PointCloudDeviceConfigurationDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PointCloudDeviceConfigurationDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"点云设备配置文件格式无效: {_configurationFilePath}", ex);
        }

        if (document?.PointCloudDevices == null || document.PointCloudDevices.Count == 0)
        {
            throw new InvalidOperationException("点云设备配置中未找到任何设备侧别。");
        }

        var result = new Dictionary<string, PointCloudDeviceSide>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < document.PointCloudDevices.Count; index++)
        {
            PointCloudDeviceConfigurationItem item = document.PointCloudDevices[index];
            if (string.IsNullOrWhiteSpace(item.SerialNumber))
            {
                throw new InvalidOperationException("点云设备配置中存在空的 SerialNumber。");
            }

            if (string.IsNullOrWhiteSpace(item.Side) ||
                !Enum.TryParse(item.Side, true, out PointCloudDeviceSide side))
            {
                throw new InvalidOperationException($"点云设备 {item.SerialNumber} 的 Side 无效，应为 Left 或 Right。");
            }

            if (!result.TryAdd(item.SerialNumber, side))
            {
                throw new InvalidOperationException($"点云设备配置中存在重复序列号: {item.SerialNumber}");
            }
        }

        return result;
    }

    public static PointCloudDeviceSide ResolveConfiguredSide(
        IReadOnlyDictionary<string, PointCloudDeviceSide> deviceSideMap,
        string serialNumber)
    {
        if (deviceSideMap == null)
        {
            throw new ArgumentNullException(nameof(deviceSideMap));
        }

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new InvalidOperationException("点云设备序列号为空，无法匹配侧别配置。");
        }

        if (!deviceSideMap.TryGetValue(serialNumber, out PointCloudDeviceSide side))
        {
            throw new InvalidOperationException($"点云设备 {serialNumber} 未配置 Left/Right 侧别。");
        }

        return side;
    }

    private sealed class PointCloudDeviceConfigurationDocument
    {
        public List<PointCloudDeviceConfigurationItem> PointCloudDevices { get; set; } = new();
    }

    private sealed class PointCloudDeviceConfigurationItem
    {
        public string SerialNumber { get; set; } = string.Empty;

        public string Side { get; set; } = string.Empty;
    }
}
