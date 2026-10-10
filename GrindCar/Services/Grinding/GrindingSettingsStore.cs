using System;
using System.IO;
using System.Text.Json;
using GrindCar.Models.Grinding;

namespace GrindCar.Services.Grinding;

public sealed class GrindingSettingsStore
{
    private readonly string _path;
    public GrindingSettingsStore(string? path = null) =>
        _path = path ?? Path.Combine(Environment.CurrentDirectory, "grinding-parameters.json");

    public GrindingSettings? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            GrindingSettings settings = JsonSerializer.Deserialize<GrindingSettings>(File.ReadAllText(_path))
                ?? throw new InvalidOperationException("配置为空。");
            GrindingSettingsValidator.BuildAll(settings);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"打磨配置加载失败：{ex.Message}", ex);
        }
    }

    public void Save(GrindingSettings settings)
    {
        GrindingSettingsValidator.BuildAll(settings);
        string path = Path.GetFullPath(_path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
