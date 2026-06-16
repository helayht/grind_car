using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GrindCar.Models.PointCloud;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class PointCloudCaptureSettingsTests
{
    [Fact]
    public void CalculateFrameRateHz_WithOneHundredFiftyMetersPerMinute_ReturnsTwoHundredFifty()
    {
        double frameRate = PointCloudCaptureSettings.CalculateFrameRateHz(150.0);

        Assert.Equal(250.0, frameRate, 6);
    }

    [Theory]
    [InlineData(0.0, 100)]
    [InlineData(-1.0, 100)]
    [InlineData(150.0, 0)]
    [InlineData(150.0, -1)]
    public void Constructor_WithInvalidValue_Throws(double speedMetersPerMinute, int profileCount)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new PointCloudCaptureSettings(speedMetersPerMinute, profileCount));

        Assert.True(
            exception.Message.Contains("小车运行速度") ||
            exception.Message.Contains("单次测量总条数"));
    }

    [Fact]
    public void Store_SaveAndLoad_ReturnsSettings()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "point-cloud-capture-settings.json");
        var store = new PointCloudCaptureSettingsStore(filePath);
        var settings = new PointCloudCaptureSettings(150.0, 400);

        store.Save(settings);
        PointCloudCaptureSettings? loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(150.0, loaded.SpeedMetersPerMinute, 6);
        Assert.Equal(400, loaded.ProfileCount);
        Assert.Equal(250.0, loaded.FrameRateHz, 6);
    }

    [Fact]
    public void Store_LoadLegacySpeedUnit_ConvertsToMetersPerMinute()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "point-cloud-capture-settings.json");
        string legacySpeedPropertyName = "Speed" + "KmPerHour";
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            "{\n" +
            $"  \"{legacySpeedPropertyName}\": 9.0,\n" +
            "  \"ProfileCount\": 400\n" +
            "}");
        var store = new PointCloudCaptureSettingsStore(filePath);

        PointCloudCaptureSettings? loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(150.0, loaded.SpeedMetersPerMinute, 6);
        Assert.Equal(400, loaded.ProfileCount);
        Assert.Equal(250.0, loaded.FrameRateHz, 6);
    }

    [Fact]
    public void LoadRequired_WithMissingFile_Throws()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "missing.json");
        var store = new PointCloudCaptureSettingsStore(filePath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.LoadRequired());

        Assert.Contains("请先在主界面设置并保存点云采集参数", exception.Message);
    }

    [Fact]
    public void PointCloudExportService_UsesVendorCaptureParameterNodeNames()
    {
        Assert.Equal("AcquisitionFrameRateEnable", PointCloudExportService.AcquisitionFrameRateEnableKey);
        Assert.Equal("AcquisitionFrameRate", PointCloudExportService.AcquisitionFrameRateKey);
        Assert.Equal("LSLRangeImgHeight", PointCloudExportService.RangeImageHeightKey);
    }

    [Fact]
    public void PointCloudExportService_UsesRangeImageCaptureMode()
    {
        Assert.Equal(7U, PointCloudExportService.CaptureImageModeValue);
    }

    [Fact]
    public void PointCloudExportService_UsesSixtySecondImageTimeout()
    {
        Assert.Equal(60000U, PointCloudExportService.DefaultGetImageTimeoutMs);
    }

    [Fact]
    public void EnsureProfileCountInRange_WithOutOfRangeValue_Throws()
    {
        PointCloudSdkException exception = Assert.Throws<PointCloudSdkException>(() =>
            PointCloudExportService.EnsureProfileCountInRange(400, 1, 256, 1));

        Assert.Contains("Y方向行数 400 超出设备支持范围 1~256", exception.Message);
    }

    [Fact]
    public void EnsureProfileCountInRange_WithInvalidIncrement_Throws()
    {
        PointCloudSdkException exception = Assert.Throws<PointCloudSdkException>(() =>
            PointCloudExportService.EnsureProfileCountInRange(7, 1, 100, 4));

        Assert.Contains("不满足设备步进 4", exception.Message);
    }

    [Fact]
    public void DecodePointCloudImage_WithShapeMatchingInt16Length_DecodesInt16Points()
    {
        short[] rawValues = { 1, 2, 3, 4, 5, 6 };
        IReadOnlyList<PointCloudPoint3D> points = DecodeInt16PointCloud(rawValues, width: 2, height: 1);

        Assert.Equal(2, points.Count);
        Assert.Equal(10.5, points[0].X, 6);
        Assert.Equal(21.0, points[0].Y, 6);
        Assert.Equal(31.5, points[0].Z, 6);
        Assert.Equal(12.0, points[1].X, 6);
        Assert.Equal(22.5, points[1].Y, 6);
        Assert.Equal(33.0, points[1].Z, 6);
    }

    [Fact]
    public void DecodePointCloudImage_WithShapeMatchingFloatLength_DecodesFloatPoints()
    {
        float[] rawValues = { 1.25f, 2.5f, 3.75f };
        IReadOnlyList<PointCloudPoint3D> points = DecodeFloatPointCloud(rawValues, width: 1, height: 1);

        Assert.Single(points);
        Assert.Equal(1.25, points[0].X, 6);
        Assert.Equal(2.5, points[0].Y, 6);
        Assert.Equal(3.75, points[0].Z, 6);
    }

    [Fact]
    public void DecodePointCloudImage_WithAmbiguousLengthAndNoShape_Throws()
    {
        IntPtr buffer = Marshal.AllocHGlobal(12);
        try
        {
            var image = new MV3D_LP_IMAGE_DATA
            {
                nWidth = 0,
                nHeight = 0,
                nDataLen = 12,
                pData = buffer
            };

            PointCloudSdkException exception = Assert.Throws<PointCloudSdkException>(() =>
                PointCloudExportService.DecodePointCloudImage(image));

            Assert.Contains("格式歧义", exception.Message);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<PointCloudPoint3D> DecodeFloatPointCloud(
        float[] rawValues,
        uint width,
        uint height)
    {
        IntPtr buffer = Marshal.AllocHGlobal(rawValues.Length * sizeof(float));
        try
        {
            Marshal.Copy(rawValues, 0, buffer, rawValues.Length);
            var image = new MV3D_LP_IMAGE_DATA
            {
                nWidth = width,
                nHeight = height,
                nDataLen = (uint)(rawValues.Length * sizeof(float)),
                pData = buffer
            };

            return PointCloudExportService.DecodePointCloudImage(image);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<PointCloudPoint3D> DecodeInt16PointCloud(
        short[] rawValues,
        uint width,
        uint height)
    {
        IntPtr buffer = Marshal.AllocHGlobal(rawValues.Length * sizeof(short));
        try
        {
            Marshal.Copy(rawValues, 0, buffer, rawValues.Length);
            var image = new MV3D_LP_IMAGE_DATA
            {
                nWidth = width,
                nHeight = height,
                nDataLen = (uint)(rawValues.Length * sizeof(short)),
                pData = buffer,
                fXScale = 0.5f,
                fYScale = 0.5f,
                fZScale = 0.5f,
                nXOffset = 10,
                nYOffset = 20,
                nZOffset = 30
            };

            return PointCloudExportService.DecodePointCloudImage(image);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
