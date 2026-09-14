using System;
using System.Collections.Generic;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Models.Rail;

/// <summary>
/// 单台廓形仪在一次采集范围内定位到的最大掉块廓形。
/// </summary>
public sealed record MaximumDropProfileResult
{
    public MaximumDropProfileResult(
        PointCloudDeviceSide side,
        int sampleIndex,
        double profileY,
        double maximumDropDepth,
        IReadOnlyList<RailProfilePoint> profilePoints)
        : this(
            side,
            sampleIndex,
            profileY,
            maximumDropDepth,
            BuildSingleSegment(profilePoints))
    {
    }

    public MaximumDropProfileResult(
        PointCloudDeviceSide side,
        int sampleIndex,
        double profileY,
        double maximumDropDepth,
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments)
        : this(
            side,
            sampleIndex,
            profileY,
            maximumDropDepth,
            profileSegments,
            ResolveMaximumDropPoint(profileSegments))
    {
    }

    public MaximumDropProfileResult(
        PointCloudDeviceSide side,
        int sampleIndex,
        double profileY,
        double maximumDropDepth,
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments,
        RailProfilePoint maximumDropPoint)
    {
        Side = side;
        SampleIndex = sampleIndex;
        ProfileY = profileY;
        MaximumDropDepth = maximumDropDepth;
        MaximumDropPoint = maximumDropPoint;
        ProfileSegments = CopySegments(profileSegments);
        ProfilePoints = FlattenSegments(ProfileSegments);
        StandardAlignmentOffset = CalculateStandardAlignmentOffset(ProfileSegments);
        ShiftedProfileSegments = ShiftSegments(ProfileSegments, StandardAlignmentOffset);
        ShiftedProfilePoints = FlattenSegments(ShiftedProfileSegments);
        ShiftedMaximumDropPoint = new RailProfilePoint(
            MaximumDropPoint.X,
            MaximumDropPoint.Y + StandardAlignmentOffset);
    }

    public PointCloudDeviceSide Side { get; }

    public int SampleIndex { get; }

    public double ProfileY { get; }

    public double MaximumDropDepth { get; }

    /// <summary>
    /// 产生最大掉块深度的原始配准、滤波后廓形点。
    /// </summary>
    public RailProfilePoint MaximumDropPoint { get; }

    /// <summary>
    /// 最大掉块点按标准对齐量上移后的显示坐标。
    /// </summary>
    public RailProfilePoint ShiftedMaximumDropPoint { get; }

    /// <summary>
    /// 使全部有效廓形点不低于同 X 坐标标准轨面的最小整体上移量。
    /// 此值独立于最大掉块深度，仅用于计算和展示时的标准廓形对齐。
    /// </summary>
    public double StandardAlignmentOffset { get; }

    /// <summary>
    /// 最终有效廓形片段的有序扁平点集，用于现有打磨深度计算。
    /// </summary>
    public IReadOnlyList<RailProfilePoint> ProfilePoints { get; }

    /// <summary>
    /// 两次最低高度过滤后保留的独立有效片段。
    /// </summary>
    public IReadOnlyList<IReadOnlyList<RailProfilePoint>> ProfileSegments { get; }

    /// <summary>
    /// 按标准廓形最小对齐量整体向上平移后的有效廓形片段。
    /// 仅用于掉块打磨深度计算和对应的对比展示，不影响掉块检测结果。
    /// </summary>
    public IReadOnlyList<IReadOnlyList<RailProfilePoint>> ShiftedProfileSegments { get; }

    /// <summary>
    /// 按标准廓形最小对齐量整体向上平移后的有序扁平点集。
    /// </summary>
    public IReadOnlyList<RailProfilePoint> ShiftedProfilePoints { get; }

    private static IReadOnlyList<IReadOnlyList<RailProfilePoint>> BuildSingleSegment(
        IReadOnlyList<RailProfilePoint> profilePoints)
    {
        if (profilePoints == null)
        {
            throw new ArgumentNullException(nameof(profilePoints));
        }

        return profilePoints.Count == 0
            ? Array.Empty<IReadOnlyList<RailProfilePoint>>()
            : new IReadOnlyList<RailProfilePoint>[] { profilePoints };
    }

    private static IReadOnlyList<IReadOnlyList<RailProfilePoint>> CopySegments(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments)
    {
        if (profileSegments == null)
        {
            throw new ArgumentNullException(nameof(profileSegments));
        }

        var copiedSegments = new List<IReadOnlyList<RailProfilePoint>>(profileSegments.Count);
        for (int segmentIndex = 0; segmentIndex < profileSegments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = profileSegments[segmentIndex] ??
                throw new ArgumentException("最大掉块廓形片段不能为空。", nameof(profileSegments));
            if (segment.Count == 0)
            {
                continue;
            }

            var copiedPoints = new RailProfilePoint[segment.Count];
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                copiedPoints[pointIndex] = segment[pointIndex];
            }

            copiedSegments.Add(copiedPoints);
        }

        return copiedSegments.AsReadOnly();
    }

    private static IReadOnlyList<RailProfilePoint> FlattenSegments(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments)
    {
        int pointCount = 0;
        for (int segmentIndex = 0; segmentIndex < profileSegments.Count; segmentIndex++)
        {
            pointCount += profileSegments[segmentIndex].Count;
        }

        var points = new List<RailProfilePoint>(pointCount);
        for (int segmentIndex = 0; segmentIndex < profileSegments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = profileSegments[segmentIndex];
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                points.Add(segment[pointIndex]);
            }
        }

        return points.AsReadOnly();
    }

    private static IReadOnlyList<IReadOnlyList<RailProfilePoint>> ShiftSegments(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments,
        double offsetY)
    {
        var shiftedSegments = new List<IReadOnlyList<RailProfilePoint>>(profileSegments.Count);
        for (int segmentIndex = 0; segmentIndex < profileSegments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = profileSegments[segmentIndex];
            var shiftedPoints = new RailProfilePoint[segment.Count];
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                RailProfilePoint point = segment[pointIndex];
                shiftedPoints[pointIndex] = new RailProfilePoint(point.X, point.Y + offsetY);
            }

            shiftedSegments.Add(shiftedPoints);
        }

        return shiftedSegments.AsReadOnly();
    }

    private static double CalculateStandardAlignmentOffset(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments)
    {
        bool hasStandardPoint = false;
        double offsetY = 0.0;
        for (int segmentIndex = 0; segmentIndex < profileSegments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = profileSegments[segmentIndex];
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                RailProfilePoint point = segment[pointIndex];
                double standardY = StandardRailProfileSolver.RailSurfaceFun(point.X);
                if (double.IsNaN(standardY) || double.IsInfinity(standardY))
                {
                    continue;
                }

                hasStandardPoint = true;
                offsetY = Math.Max(offsetY, standardY - point.Y);
            }
        }

        if (!hasStandardPoint)
        {
            throw new InvalidOperationException("最大掉块廓形中没有可用于标准轨面对齐的有效点。");
        }

        return offsetY;
    }

    private static RailProfilePoint ResolveMaximumDropPoint(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> profileSegments)
    {
        if (profileSegments == null)
        {
            throw new ArgumentNullException(nameof(profileSegments));
        }

        bool hasCandidate = false;
        double minimumResidual = double.PositiveInfinity;
        RailProfilePoint maximumDropPoint = default;
        for (int segmentIndex = 0; segmentIndex < profileSegments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = profileSegments[segmentIndex] ??
                throw new ArgumentException("最大掉块廓形片段不能为空。", nameof(profileSegments));
            for (int pointIndex = 0; pointIndex < segment.Count; pointIndex++)
            {
                RailProfilePoint point = segment[pointIndex];
                double standardY = StandardRailProfileSolver.RailSurfaceFun(point.X);
                if (double.IsNaN(standardY) || double.IsInfinity(standardY))
                {
                    continue;
                }

                double residual = point.Y - standardY;
                if (!hasCandidate || residual < minimumResidual)
                {
                    hasCandidate = true;
                    minimumResidual = residual;
                    maximumDropPoint = point;
                }
            }
        }

        if (!hasCandidate)
        {
            throw new InvalidOperationException("最大掉块廓形中没有可用于定位最大深度的有效点。");
        }

        return maximumDropPoint;
    }
}
