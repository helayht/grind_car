using System;
using System.Collections.Generic;
using System.Globalization;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 打磨角度输入解析。
/// </summary>
public static class GrindDepthAngleParser
{
    public static IReadOnlyList<int> ParseAngles(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("请输入至少一个打磨角度。");
        }

        string normalizedText = input
            .Replace('，', ',')
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        string[] tokens = normalizedText
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("请输入至少一个有效的打磨角度。");
        }

        var angles = new List<int>(tokens.Length);
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int angle) &&
                !int.TryParse(token, NumberStyles.Integer, CultureInfo.CurrentCulture, out angle))
            {
                throw new InvalidOperationException($"无法解析角度值: {token}");
            }

            angles.Add(angle);
        }

        return angles;
    }
}