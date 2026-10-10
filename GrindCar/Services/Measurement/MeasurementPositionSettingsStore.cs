using System;
using System.IO;
using System.Text.Json;
using GrindCar.Models.Measurement;

namespace GrindCar.Services.Measurement;

public sealed class MeasurementPositionSettingsStore
{
    private const int CurrentVersion = 1;
    private readonly string _path;
    public MeasurementPositionSettingsStore(string? path = null) =>
        _path = path ?? Path.Combine(Environment.CurrentDirectory, "measurement-parameters.json");

    public MeasurementPositions? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            SettingsDocument? document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(_path));
            if (document == null || document.Version != CurrentVersion || !document.Start.HasValue || !document.End.HasValue ||
                !document.ProfilerPosition.HasValue || !document.AvoidancePosition.HasValue)
                throw new InvalidOperationException("配置版本不兼容或位置参数缺失。");
            var positions = new MeasurementPositions(document.Start.Value, document.End.Value, document.ProfilerPosition.Value, document.AvoidancePosition.Value);
            positions.Validate();
            return positions;
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            throw new InvalidOperationException($"测量位置配置加载失败：{ex.Message}", ex);
        }
    }

    public void Save(MeasurementPositions positions)
    {
        positions.Validate();
        string path = Path.GetFullPath(_path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var document = new SettingsDocument
            {
                Version = CurrentVersion, Start = positions.Start, End = positions.End,
                ProfilerPosition = positions.ProfilerPosition, AvoidancePosition = positions.AvoidancePosition
            };
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class SettingsDocument
    {
        public int Version { get; set; }
        public double? Start { get; set; }
        public double? End { get; set; }
        public double? ProfilerPosition { get; set; }
        public double? AvoidancePosition { get; set; }
    }
}
