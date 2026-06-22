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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _configurationFilePath;

    public ProfileRegistrationSettingsStore(string? configurationFilePath = null)
    {
        _configurationFilePath = string.IsNullOrWhiteSpace(configurationFilePath)
            ? Path.Combine(Environment.CurrentDirectory, DefaultFileName)
            : configurationFilePath;
    }

    public string ConfigurationFilePath => _configurationFilePath;

    public ProfileRegistrationSettings? Load()
    {
        if (!File.Exists(_configurationFilePath))
        {
            return null;
        }

        string json = File.ReadAllText(_configurationFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"代表廓形配准配置文件为空: {_configurationFilePath}");
        }

        ProfileRegistrationSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<ProfileRegistrationSettings>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"代表廓形配准配置文件格式无效: {_configurationFilePath}", ex);
        }

        if (settings == null)
        {
            throw new InvalidOperationException($"代表廓形配准配置文件格式无效: {_configurationFilePath}");
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
        string? directoryPath = Path.GetDirectoryName(_configurationFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_configurationFilePath, json);
    }
}
