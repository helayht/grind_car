using System;
using System.Collections.Generic;
using System.IO;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudDeviceConfigurationStoreTests
{
    [Fact]
    public void LoadDeviceSideMap_WithValidConfiguration_ReturnsSides()
    {
        string configurationPath = CreateConfigurationFile(
            @"{
              ""PointCloudDevices"": [
                { ""SerialNumber"": ""LEFT-001"", ""Side"": ""Left"" },
                { ""SerialNumber"": ""RIGHT-001"", ""Side"": ""Right"" }
              ]
            }");

        var store = new PointCloudDeviceConfigurationStore(configurationPath);

        var result = store.LoadDeviceSideMap();

        Assert.Equal(PointCloudDeviceSide.Left, result["LEFT-001"]);
        Assert.Equal(PointCloudDeviceSide.Right, result["RIGHT-001"]);
    }

    [Fact]
    public void LoadDeviceSideMap_WithMissingFile_Throws()
    {
        string configurationPath = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "missing.json");
        var store = new PointCloudDeviceConfigurationStore(configurationPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.LoadDeviceSideMap());

        Assert.Contains("配置文件不存在", exception.Message);
    }

    [Fact]
    public void LoadDeviceSideMap_WithDuplicateSerialNumber_Throws()
    {
        string configurationPath = CreateConfigurationFile(
            @"{
              ""PointCloudDevices"": [
                { ""SerialNumber"": ""DUP-001"", ""Side"": ""Left"" },
                { ""SerialNumber"": ""DUP-001"", ""Side"": ""Right"" }
              ]
            }");
        var store = new PointCloudDeviceConfigurationStore(configurationPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.LoadDeviceSideMap());

        Assert.Contains("重复序列号", exception.Message);
    }

    [Fact]
    public void LoadDeviceSideMap_WithInvalidSide_Throws()
    {
        string configurationPath = CreateConfigurationFile(
            @"{
              ""PointCloudDevices"": [
                { ""SerialNumber"": ""BAD-001"", ""Side"": ""Center"" }
              ]
            }");
        var store = new PointCloudDeviceConfigurationStore(configurationPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.LoadDeviceSideMap());

        Assert.Contains("Side 无效", exception.Message);
    }

    [Fact]
    public void ResolveConfiguredSide_WithUnconfiguredDevice_Throws()
    {
        var deviceSideMap = new Dictionary<string, PointCloudDeviceSide>(StringComparer.OrdinalIgnoreCase)
        {
            ["KNOWN-001"] = PointCloudDeviceSide.Left
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            PointCloudDeviceConfigurationStore.ResolveConfiguredSide(deviceSideMap, "UNKNOWN-001"));

        Assert.Contains("未配置 Left/Right 侧别", exception.Message);
    }

    private static string CreateConfigurationFile(string content)
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        string configurationPath = Path.Combine(directoryPath, "point-cloud-devices.json");
        File.WriteAllText(configurationPath, content);
        return configurationPath;
    }
}
