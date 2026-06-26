using System;
using System.IO;
using System.Text.Json;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 代表廓形手动配准参数存储。
/// </summary>
public sealed class ProfileRegistrationSettingsStore
{
    private const string DefaultFileName = "point-cloud-profile-registration.json";
    private const string Kg60FileName = "point-cloud-profile-registration-60kg.json";
    private const string Kg50FileName = "point-cloud-profile-registration-50kg.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _configurationFilePath;
    private readonly bool _isExplicitPath;

    public ProfileRegistrationSettingsStore(string? configurationFilePath = null)
    {
        _isExplicitPath = !string.IsNullOrWhiteSpace(configurationFilePath);
        _configurationFilePath = _isExplicitPath
            ? configurationFilePath!
            : Path.Combine(Environment.CurrentDirectory, DefaultFileName);
    }

    public string ConfigurationFilePath => _configurationFilePath;

    /// <summary>
    /// 获取当前激活轨型对应的配置文件路径。
    /// 当未指定显式路径时，自动根据当前轨型推导文件名。
    /// </summary>
    public string ResolveConfigurationFilePath()
    {
        if (_isExplicitPath)
        {
            return _configurationFilePath;
        }

        string fileName = StandardRailProfileSolver.CurrentProfileType switch
        {
            RailProfileType.Kg60 => Kg60FileName,
            RailProfileType.Kg50 => Kg50FileName,
            _ => DefaultFileName
        };

        return Path.Combine(Environment.CurrentDirectory, fileName);
    }

    public ProfileRegistrationSettings? Load()
    {
        string filePath = ResolveConfigurationFilePath();
        if (!File.Exists(filePath))
        {
            return null;
        }

        string json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"代表廓形配准配置文件为空: {filePath}");
        }

        ProfileRegistrationSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<ProfileRegistrationSettings>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"代表廓形配准配置文件格式无效: {filePath}", ex);
        }

        if (settings == null)
        {
            throw new InvalidOperationException($"代表廓形配准配置文件格式无效: {filePath}");
        }

        settings.Validate();
        return settings;
    }

    public ProfileRegistrationSettings LoadRequired()
    {
        ProfileRegistrationSettings? settings = Load();
        if (settings == null)
        {
            throw new InvalidOperationException("请先完成代表廓形手动配准并保存参数。");
        }

        return settings;
    }

    public void Save(ProfileRegistrationSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.Validate();
        string filePath = ResolveConfigurationFilePath();
        string? directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(filePath, json);
    }
}
