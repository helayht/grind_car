using System;
using System.IO;
using System.Text.Json;
using GrindCar.Models.PointCloud;

namespace GrindCar.Services.PointCloud;

/// <summary>
/// 点云在线采集参数存储。
/// </summary>
public sealed class PointCloudCaptureSettingsStore
{
    private const string DefaultFileName = "point-cloud-capture-settings.json";
    private const double MetersPerKilometer = 1000.0;
    private const double MinutesPerHour = 60.0;
    private static readonly string LegacySpeedPropertyName = "Speed" + "KmPerHour";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _configurationFilePath;

    public PointCloudCaptureSettingsStore(string? configurationFilePath = null)
    {
        _configurationFilePath = string.IsNullOrWhiteSpace(configurationFilePath)
            ? Path.Combine(Environment.CurrentDirectory, DefaultFileName)
            : configurationFilePath;
    }

    public string ConfigurationFilePath => _configurationFilePath;

    public PointCloudCaptureSettings? Load()
    {
        if (!File.Exists(_configurationFilePath))
        {
            return null;
        }

        string json = File.ReadAllText(_configurationFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"点云采集参数配置文件为空: {_configurationFilePath}");
        }

        PointCloudCaptureSettingsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PointCloudCaptureSettingsDocument>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"点云采集参数配置文件格式无效: {_configurationFilePath}", ex);
        }

        if (document == null)
        {
            throw new InvalidOperationException($"点云采集参数配置文件格式无效: {_configurationFilePath}");
        }

        double speedMetersPerMinute = document.SpeedMetersPerMinute > 0.0
            ? document.SpeedMetersPerMinute
            : ReadLegacySpeed(json) * MetersPerKilometer / MinutesPerHour;

        return new PointCloudCaptureSettings(speedMetersPerMinute, document.ProfileCount);
    }

    public PointCloudCaptureSettings LoadRequired()
    {
        PointCloudCaptureSettings? settings = Load();
        if (settings == null)
        {
            throw new InvalidOperationException("请先在主界面设置并保存点云采集参数。");
        }

        return settings;
    }

    public void Save(PointCloudCaptureSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        string? directoryPath = Path.GetDirectoryName(_configurationFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var document = new PointCloudCaptureSettingsDocument
        {
            SpeedMetersPerMinute = settings.SpeedMetersPerMinute,
            ProfileCount = settings.ProfileCount
        };
        string json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(_configurationFilePath, json);
    }

    private sealed class PointCloudCaptureSettingsDocument
    {
        public double SpeedMetersPerMinute { get; set; }

        public int ProfileCount { get; set; }
    }

    private static double ReadLegacySpeed(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(LegacySpeedPropertyName, out JsonElement legacySpeed))
        {
            return legacySpeed.GetDouble();
        }

        return 0.0;
    }
}
