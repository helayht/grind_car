using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Measurement;

/// <summary>一次完整测量组的独立代表点快照。</summary>
public sealed class MeasurementRepresentativeProfile
{
    public MeasurementRepresentativeProfile(int sampleIndex, IReadOnlyList<RailProfilePoint> points)
    {
        SampleIndex = sampleIndex;
        Points = System.Array.AsReadOnly(points.ToArray());
    }

    public int SampleIndex { get; }
    public IReadOnlyList<RailProfilePoint> Points { get; }
    public string DisplayName => $"第{SampleIndex}组";
}
