using System;
using System.IO;
using System.Text.Json;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 打磨深度检测基线持久化服务。
/// </summary>
public static class GrindingDepthBaselineStore
{
    private const string DefaultLogDirectoryName = "Log";
    private const string DefaultBaselineFileName = "grind-depth-baseline.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static void Save(GrindingDepthBaseline baseline, string? baselineFilePath = null)
    {
        if (baseline == null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        string resolvedPath = ResolveBaselineFilePath(baselineFilePath);
        string? directoryPath = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("基线文件目录无效。");
        }

        Directory.CreateDirectory(directoryPath);
        string json = JsonSerializer.Serialize(baseline, SerializerOptions);
        File.WriteAllText(resolvedPath, json);
    }

    public static GrindingDepthBaseline? LoadLatest(string? baselineFilePath = null)
    {
        string resolvedPath = ResolveBaselineFilePath(baselineFilePath);
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        string json = File.ReadAllText(resolvedPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<GrindingDepthBaseline>(json, SerializerOptions);
    }

    private static string ResolveBaselineFilePath(string? baselineFilePath)
    {
        return string.IsNullOrWhiteSpace(baselineFilePath)
            ? Path.Combine(Environment.CurrentDirectory, DefaultLogDirectoryName, DefaultBaselineFileName)
            : baselineFilePath;
    }
}