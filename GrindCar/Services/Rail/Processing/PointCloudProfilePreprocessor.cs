using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 点云代表廓形与最大掉块分析共用的预处理入口。
/// </summary>
internal interface IPointCloudProfilePreprocessor
{
    PreparedPointCloudProfileData Prepare(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide? side = null,
        ProfileRegistrationParameters? explicitParameters = null);
}

/// <summary>
/// 已完成有效点过滤、方向校正和 Y 分组的只读点云数据。
/// </summary>
internal sealed class PreparedPointCloudProfileData
{
    public PreparedPointCloudProfileData(
        PointCloudDeviceSide? side,
        ProfileRegistrationParameters? registrationParameters,
        double representativeY,
        IReadOnlyList<IReadOnlyList<PointCloudPoint3D>> sections)
    {
        Side = side;
        RegistrationParameters = registrationParameters;
        RepresentativeY = representativeY;
        Sections = sections ?? throw new ArgumentNullException(nameof(sections));
    }

    public PointCloudDeviceSide? Side { get; }

    public ProfileRegistrationParameters? RegistrationParameters { get; }

    public double RepresentativeY { get; }

    public IReadOnlyList<IReadOnlyList<PointCloudPoint3D>> Sections { get; }
}

/// <summary>
/// 统一完成有效点过滤、配准方向变换、代表 Y 计算和截面分组。
/// </summary>
internal sealed class PointCloudProfilePreprocessor : IPointCloudProfilePreprocessor
{
    internal const double YSplitToleranceMm = 0.25;
    private const double CoordinateTolerance = 1e-7;

    private readonly ProfileRegistrationSettingsStore _registrationSettingsStore;
    private readonly ProfileRegistrationTransformService _registrationTransformService;

    public PointCloudProfilePreprocessor()
        : this(new ProfileRegistrationSettingsStore(), new ProfileRegistrationTransformService())
    {
    }

    public PointCloudProfilePreprocessor(
        ProfileRegistrationSettingsStore registrationSettingsStore,
        ProfileRegistrationTransformService registrationTransformService)
    {
        _registrationSettingsStore = registrationSettingsStore ??
            throw new ArgumentNullException(nameof(registrationSettingsStore));
        _registrationTransformService = registrationTransformService ??
            throw new ArgumentNullException(nameof(registrationTransformService));
    }

    public PreparedPointCloudProfileData Prepare(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide? side = null,
        ProfileRegistrationParameters? explicitParameters = null)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (!side.HasValue && explicitParameters != null)
        {
            throw new ArgumentException("未指定设备侧别时不能应用配准参数。", nameof(explicitParameters));
        }

        List<PointCloudPoint3D> validPoints = FilterValidPoints(points);
        if (validPoints.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云数据中未解析到有效坐标点。");
        }

        double representativeY = ResolveRepresentativeY(validPoints);
        ProfileRegistrationParameters? registrationParameters = null;
        IReadOnlyList<PointCloudPoint3D> orientedPoints = validPoints;
        if (side.HasValue)
        {
            registrationParameters = explicitParameters ??
                _registrationSettingsStore.LoadRequired().GetParameters(side.Value);
            orientedPoints = _registrationTransformService.ApplyRawOrientation(
                validPoints,
                registrationParameters,
                side.Value);
        }

        IReadOnlyList<IReadOnlyList<PointCloudPoint3D>> sections = SplitSectionsByY(orientedPoints);
        return new PreparedPointCloudProfileData(
            side,
            registrationParameters,
            representativeY,
            sections);
    }

    private static List<PointCloudPoint3D> FilterValidPoints(IReadOnlyList<PointCloudPoint3D> points)
    {
        var validPoints = new List<PointCloudPoint3D>(points.Count);
        for (int index = 0; index < points.Count; index++)
        {
            PointCloudPoint3D point = points[index];
            if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z) || IsZeroPoint(point))
            {
                continue;
            }

            validPoints.Add(point);
        }

        return validPoints;
    }

    private static double ResolveRepresentativeY(IReadOnlyList<PointCloudPoint3D> points)
    {
        var uniqueYSet = new HashSet<double>();
        for (int index = 0; index < points.Count; index++)
        {
            uniqueYSet.Add(points[index].Y);
        }

        if (uniqueYSet.Count == 0)
        {
            throw new RepresentativeProfileExtractionException("点云数据中未解析到有效的 Y 坐标。");
        }

        double[] sortedUniqueY = new double[uniqueYSet.Count];
        uniqueYSet.CopyTo(sortedUniqueY);
        Array.Sort(sortedUniqueY);

        int middleIndex = sortedUniqueY.Length / 2;
        return sortedUniqueY.Length % 2 == 0
            ? (sortedUniqueY[middleIndex - 1] + sortedUniqueY[middleIndex]) / 2.0
            : sortedUniqueY[middleIndex];
    }

    private static IReadOnlyList<IReadOnlyList<PointCloudPoint3D>> SplitSectionsByY(
        IReadOnlyList<PointCloudPoint3D> points)
    {
        List<PointCloudPoint3D> sortedPoints = new(points);
        sortedPoints.Sort((left, right) =>
        {
            int yComparison = left.Y.CompareTo(right.Y);
            return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
        });

        var mutableSections = new List<List<PointCloudPoint3D>>();
        List<PointCloudPoint3D>? currentSection = null;
        double currentY = 0.0;
        for (int index = 0; index < sortedPoints.Count; index++)
        {
            PointCloudPoint3D point = sortedPoints[index];
            if (currentSection == null || Math.Abs(point.Y - currentY) > YSplitToleranceMm)
            {
                currentSection = new List<PointCloudPoint3D>();
                mutableSections.Add(currentSection);
                currentY = point.Y;
            }

            currentSection.Add(point);
        }

        var sections = new List<IReadOnlyList<PointCloudPoint3D>>(mutableSections.Count);
        for (int sectionIndex = 0; sectionIndex < mutableSections.Count; sectionIndex++)
        {
            List<PointCloudPoint3D> section = mutableSections[sectionIndex];
            section.Sort((left, right) => left.X.CompareTo(right.X));
            sections.Add(section.AsReadOnly());
        }

        return sections.AsReadOnly();
    }

    private static bool IsZeroPoint(PointCloudPoint3D point)
    {
        return Math.Abs(point.X) < CoordinateTolerance &&
               Math.Abs(point.Y) < CoordinateTolerance &&
               Math.Abs(point.Z) < CoordinateTolerance;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
