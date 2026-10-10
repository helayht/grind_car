using System.Collections.Generic;

namespace GrindCar.Models.Grinding;

public sealed record GrindingParameterDefinition(
    string Key, string Name, ushort BaseAddress, decimal Scale, string Unit,
    bool IsInt16 = false, bool IsInteger = false, bool NonNegative = false, bool Positive = false)
{
    public ushort Address(int index = 0) => checked((ushort)(BaseAddress + index * (IsInt16 ? 1 : 2)));
}

public sealed class GrindingAngleSettings
{
    public int Number { get; set; }
    public int Angle { get; set; }
    public Dictionary<string, decimal> Values { get; set; } = new();
}

public sealed class GrindingSettings
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, decimal> Basic { get; set; } = new();
    public List<GrindingAngleSettings> Angles { get; set; } = new();
}
