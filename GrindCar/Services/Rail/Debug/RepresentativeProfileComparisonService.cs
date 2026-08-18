using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GrindCar.Models.Rail;
using GrindCar.Services;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 代表廓形曲线的绘制方式。
/// </summary>
public enum RepresentativeProfileCurveStyle
{
    Smooth,
    Polyline
}

/// <summary>
/// 代表轨面与标准轨面对比计算服务。
/// </summary>
public sealed class RepresentativeProfileComparisonService
{
    private const double PlotPadding = 36.0;
    private const double PointDiameter = 5.0;
    private const int RepresentativeInterpolationSteps = 10;
    private const int StandardSampleCount = 240;

    /// <summary>
    /// 基于代表点构建对比展示所需的静态数据。
    /// </summary>
    /// <param name="representativePoints">代表截面点。</param>
    /// <returns>对比展示数据快照。</returns>
    public RepresentativeProfileComparisonSnapshot BuildSnapshot(IReadOnlyList<RailProfilePoint> representativePoints)
    {
        if (representativePoints == null)
        {
            throw new ArgumentNullException(nameof(representativePoints));
        }

        return BuildSnapshot(new IReadOnlyList<RailProfilePoint>[] { representativePoints });
    }

    /// <summary>
    /// 基于多个独立代表点片段构建对比展示所需的静态数据。
    /// </summary>
    public RepresentativeProfileComparisonSnapshot BuildSnapshot(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> representativeSegments)
    {
        if (representativeSegments == null)
        {
            throw new ArgumentNullException(nameof(representativeSegments));
        }

        IReadOnlyList<IReadOnlyList<RailProfilePoint>> alignedSegments =
            AlignSegments(representativeSegments, nameof(representativeSegments));
        IReadOnlyList<RailProfilePoint> alignedRepresentativePoints = FlattenAndSort(alignedSegments);
        if (alignedRepresentativePoints.Count < 2)
        {
            throw new InvalidOperationException("代表点数量不足，无法绘制曲线。");
        }

        IReadOnlyList<RailProfilePoint> standardPoints = BuildStandardPoints(alignedRepresentativePoints);
        RepresentativeProfileBounds bounds = BuildBounds(alignedRepresentativePoints, standardPoints);

        IEnumerable<RailProfilePoint> allPoints = alignedRepresentativePoints.Concat(standardPoints);
        string pointCountText = alignedRepresentativePoints.Count.ToString(CultureInfo.InvariantCulture);
        string xRangeText = $"{allPoints.Min(point => point.X):0.###} ~ {allPoints.Max(point => point.X):0.###}";
        string yRangeText = $"{allPoints.Min(point => point.Y):0.###} ~ {allPoints.Max(point => point.Y):0.###}";

        return new RepresentativeProfileComparisonSnapshot(
            alignedRepresentativePoints,
            alignedSegments,
            standardPoints,
            bounds,
            pointCountText,
            xRangeText,
            yRangeText);
    }

    /// <summary>
    /// 基于 Left/Right 各自的最大掉块廓形构建同坐标系展示快照。
    /// </summary>
    public MaximumDropProfileComparisonSnapshot BuildMaximumDropSnapshot(
        MaximumDropProfileResult? leftMaximumDropProfile,
        MaximumDropProfileResult? rightMaximumDropProfile)
    {
        ValidateMaximumDropSide(
            leftMaximumDropProfile,
            PointCloudDeviceSide.Left,
            nameof(leftMaximumDropProfile));
        ValidateMaximumDropSide(
            rightMaximumDropProfile,
            PointCloudDeviceSide.Right,
            nameof(rightMaximumDropProfile));

        if (leftMaximumDropProfile == null && rightMaximumDropProfile == null)
        {
            throw new InvalidOperationException("至少需要一侧有效的最大掉块廓形。");
        }

        IReadOnlyList<IReadOnlyList<RailProfilePoint>> leftSegments =
            leftMaximumDropProfile == null
                ? Array.Empty<IReadOnlyList<RailProfilePoint>>()
                : AlignSegments(
                    leftMaximumDropProfile.ProfileSegments,
                    nameof(leftMaximumDropProfile));
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> rightSegments =
            rightMaximumDropProfile == null
                ? Array.Empty<IReadOnlyList<RailProfilePoint>>()
                : AlignSegments(
                    rightMaximumDropProfile.ProfileSegments,
                    nameof(rightMaximumDropProfile));
        IReadOnlyList<RailProfilePoint> leftPoints = FlattenAndSort(leftSegments);
        IReadOnlyList<RailProfilePoint> rightPoints = FlattenAndSort(rightSegments);
        IReadOnlyList<RailProfilePoint> combinedPoints = leftPoints
            .Concat(rightPoints)
            .OrderBy(point => point.X)
            .ToArray();
        if (combinedPoints.Count < 2)
        {
            throw new InvalidOperationException("最大掉块廓形点数量不足，无法绘制曲线。");
        }

        IReadOnlyList<RailProfilePoint> standardPoints = BuildStandardPoints(combinedPoints);
        RepresentativeProfileBounds bounds = BuildBounds(combinedPoints, standardPoints);
        IEnumerable<RailProfilePoint> allPoints = combinedPoints.Concat(standardPoints);

        return new MaximumDropProfileComparisonSnapshot(
            leftPoints,
            leftSegments,
            rightPoints,
            rightSegments,
            standardPoints,
            bounds,
            $"Left {leftPoints.Count.ToString(CultureInfo.InvariantCulture)} / Right {rightPoints.Count.ToString(CultureInfo.InvariantCulture)}",
            $"{allPoints.Min(point => point.X):0.###} ~ {allPoints.Max(point => point.X):0.###}",
            $"{allPoints.Min(point => point.Y):0.###} ~ {allPoints.Max(point => point.Y):0.###}");
    }

    /// <summary>
    /// 根据当前绘图区尺寸构建曲线和坐标轴绘制数据。
    /// </summary>
    /// <param name="snapshot">对比展示静态数据。</param>
    /// <param name="plotWidth">绘图区宽度。</param>
    /// <param name="plotHeight">绘图区高度。</param>
    /// <returns>绘制结果。</returns>
    public RepresentativeProfilePlotResult BuildPlot(
        RepresentativeProfileComparisonSnapshot snapshot,
        double plotWidth,
        double plotHeight,
        RepresentativeProfileCurveStyle curveStyle = RepresentativeProfileCurveStyle.Smooth)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (plotWidth <= PlotPadding * 2.0 || plotHeight <= PlotPadding * 2.0)
        {
            return RepresentativeProfilePlotResult.Empty;
        }

        IReadOnlyList<Point> representativeScreenPoints =
            MapToScreen(snapshot.AlignedRepresentativePoints, snapshot.Bounds, plotWidth, plotHeight);
        IReadOnlyList<IReadOnlyList<Point>> representativeScreenSegments =
            MapSegmentsToScreen(snapshot.AlignedRepresentativeSegments, snapshot.Bounds, plotWidth, plotHeight);
        IReadOnlyList<Point> standardScreenPoints =
            MapToScreen(snapshot.StandardPoints, snapshot.Bounds, plotWidth, plotHeight);

        double xAxisY = MapY(0.0, snapshot.Bounds, plotHeight);
        double yAxisX = MapX(0.0, snapshot.Bounds, plotWidth);

        IReadOnlyList<RepresentativeProfileScreenPoint> pointItems =
            representativeScreenPoints
                .Select(point => new RepresentativeProfileScreenPoint(
                    point.X - PointDiameter / 2.0,
                    point.Y - PointDiameter / 2.0))
                .ToArray();

        Geometry representativeCurveGeometry = curveStyle == RepresentativeProfileCurveStyle.Polyline
            ? BuildSegmentedPolylineGeometry(representativeScreenSegments)
            : BuildSmoothGeometry(representativeScreenPoints);

        return new RepresentativeProfilePlotResult(
            representativeCurveGeometry,
            BuildPolylineGeometry(standardScreenPoints),
            pointItems,
            PlotPadding,
            plotWidth - PlotPadding,
            xAxisY,
            xAxisY,
            yAxisX,
            yAxisX,
            PlotPadding,
            plotHeight - PlotPadding);
    }

    /// <summary>
    /// 将 Left/Right 最大掉块廓形映射到同一绘图坐标系。
    /// </summary>
    public MaximumDropProfilePlotResult BuildMaximumDropPlot(
        MaximumDropProfileComparisonSnapshot snapshot,
        double plotWidth,
        double plotHeight)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (plotWidth <= PlotPadding * 2.0 || plotHeight <= PlotPadding * 2.0)
        {
            return MaximumDropProfilePlotResult.Empty;
        }

        IReadOnlyList<Point> leftScreenPoints =
            MapToScreen(snapshot.AlignedLeftPoints, snapshot.Bounds, plotWidth, plotHeight);
        IReadOnlyList<IReadOnlyList<Point>> leftScreenSegments =
            MapSegmentsToScreen(snapshot.AlignedLeftSegments, snapshot.Bounds, plotWidth, plotHeight);
        IReadOnlyList<Point> rightScreenPoints =
            MapToScreen(snapshot.AlignedRightPoints, snapshot.Bounds, plotWidth, plotHeight);
        IReadOnlyList<IReadOnlyList<Point>> rightScreenSegments =
            MapSegmentsToScreen(snapshot.AlignedRightSegments, snapshot.Bounds, plotWidth, plotHeight);
        IReadOnlyList<Point> standardScreenPoints =
            MapToScreen(snapshot.StandardPoints, snapshot.Bounds, plotWidth, plotHeight);

        double xAxisY = MapY(0.0, snapshot.Bounds, plotHeight);
        double yAxisX = MapX(0.0, snapshot.Bounds, plotWidth);

        return new MaximumDropProfilePlotResult(
            BuildSegmentedPolylineGeometry(leftScreenSegments),
            BuildSegmentedPolylineGeometry(rightScreenSegments),
            BuildPolylineGeometry(standardScreenPoints),
            BuildPointItems(leftScreenPoints),
            BuildPointItems(rightScreenPoints),
            PlotPadding,
            plotWidth - PlotPadding,
            xAxisY,
            xAxisY,
            yAxisX,
            yAxisX,
            PlotPadding,
            plotHeight - PlotPadding);
    }

    private static IReadOnlyList<IReadOnlyList<RailProfilePoint>> AlignSegments(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments,
        string parameterName)
    {
        var alignedSegments = new List<IReadOnlyList<RailProfilePoint>>(segments.Count);
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            IReadOnlyList<RailProfilePoint> segment = segments[segmentIndex] ??
                throw new ArgumentException("廓形点片段不能为空。", parameterName);
            if (segment.Count == 0)
            {
                continue;
            }

            alignedSegments.Add(segment.OrderBy(point => point.X).ToArray());
        }

        return alignedSegments.AsReadOnly();
    }

    private static IReadOnlyList<RailProfilePoint> FlattenAndSort(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments)
    {
        return segments
            .SelectMany(segment => segment)
            .OrderBy(point => point.X)
            .ToArray();
    }

    private static IReadOnlyList<RepresentativeProfileScreenPoint> BuildPointItems(
        IReadOnlyList<Point> screenPoints)
    {
        return screenPoints
            .Select(point => new RepresentativeProfileScreenPoint(
                point.X - PointDiameter / 2.0,
                point.Y - PointDiameter / 2.0))
            .ToArray();
    }

    private static void ValidateMaximumDropSide(
        MaximumDropProfileResult? maximumDropProfile,
        PointCloudDeviceSide expectedSide,
        string parameterName)
    {
        if (maximumDropProfile != null && maximumDropProfile.Side != expectedSide)
        {
            throw new ArgumentException(
                $"最大掉块廓形侧别应为 {expectedSide}。",
                parameterName);
        }
    }

    private static IReadOnlyList<Point> MapToScreen(
        IReadOnlyList<RailProfilePoint> points,
        RepresentativeProfileBounds bounds,
        double plotWidth,
        double plotHeight)
    {
        return points
            .Select(point => new Point(
                MapX(point.X, bounds, plotWidth),
                MapY(point.Y, bounds, plotHeight)))
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<Point>> MapSegmentsToScreen(
        IReadOnlyList<IReadOnlyList<RailProfilePoint>> segments,
        RepresentativeProfileBounds bounds,
        double plotWidth,
        double plotHeight)
    {
        var screenSegments = new List<IReadOnlyList<Point>>(segments.Count);
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            screenSegments.Add(MapToScreen(segments[segmentIndex], bounds, plotWidth, plotHeight));
        }

        return screenSegments.AsReadOnly();
    }

    private static double MapX(double x, RepresentativeProfileBounds bounds, double plotWidth)
    {
        double width = plotWidth - PlotPadding * 2.0;
        double xRange = bounds.MaxX - bounds.MinX;
        double safeRange = xRange <= 0.0 ? 1.0 : xRange;
        return PlotPadding + (x - bounds.MinX) / safeRange * width;
    }

    private static double MapY(double y, RepresentativeProfileBounds bounds, double plotHeight)
    {
        double height = plotHeight - PlotPadding * 2.0;
        double yRange = bounds.MaxY - bounds.MinY;
        double safeRange = yRange <= 0.0 ? 1.0 : yRange;
        return PlotPadding + (bounds.MaxY - y) / safeRange * height;
    }

    private static Geometry BuildPolylineGeometry(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(points[0], false, false);
        for (int index = 1; index < points.Count; index++)
        {
            context.LineTo(points[index], true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildSegmentedPolylineGeometry(
        IReadOnlyList<IReadOnlyList<Point>> segments)
    {
        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();
        bool hasPoints = false;
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            IReadOnlyList<Point> segment = segments[segmentIndex];
            if (segment.Count == 0)
            {
                continue;
            }

            hasPoints = true;
            context.BeginFigure(segment[0], false, false);
            for (int pointIndex = 1; pointIndex < segment.Count; pointIndex++)
            {
                context.LineTo(segment[pointIndex], true, false);
            }
        }

        if (!hasPoints)
        {
            return Geometry.Empty;
        }

        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildSmoothGeometry(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        if (points.Count < 3)
        {
            return BuildPolylineGeometry(points);
        }

        Point[] interpolatedPoints = BuildCatmullRomPoints(points).ToArray();
        return BuildPolylineGeometry(interpolatedPoints);
    }

    private static IEnumerable<Point> BuildCatmullRomPoints(IReadOnlyList<Point> points)
    {
        yield return points[0];

        for (int index = 0; index < points.Count - 1; index++)
        {
            Point p0 = index == 0 ? points[index] : points[index - 1];
            Point p1 = points[index];
            Point p2 = points[index + 1];
            Point p3 = index + 2 < points.Count ? points[index + 2] : points[index + 1];

            for (int step = 1; step <= RepresentativeInterpolationSteps; step++)
            {
                double t = step / (double)RepresentativeInterpolationSteps;
                yield return InterpolateCatmullRom(p0, p1, p2, p3, t);
            }
        }
    }

    private static Point InterpolateCatmullRom(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x =
            0.5 * ((2.0 * p1.X) +
                   (-p0.X + p2.X) * t +
                   (2.0 * p0.X - 5.0 * p1.X + 4.0 * p2.X - p3.X) * t2 +
                   (-p0.X + 3.0 * p1.X - 3.0 * p2.X + p3.X) * t3);

        double y =
            0.5 * ((2.0 * p1.Y) +
                   (-p0.Y + p2.Y) * t +
                   (2.0 * p0.Y - 5.0 * p1.Y + 4.0 * p2.Y - p3.Y) * t2 +
                   (-p0.Y + 3.0 * p1.Y - 3.0 * p2.Y + p3.Y) * t3);

        return new Point(x, y);
    }

    private static IReadOnlyList<RailProfilePoint> BuildStandardPoints(
        IReadOnlyList<RailProfilePoint> alignedRepresentativePoints)
    {
        double minX = alignedRepresentativePoints[0].X;
        double maxX = alignedRepresentativePoints[alignedRepresentativePoints.Count - 1].X;

        var standardPoints = new List<RailProfilePoint>(StandardSampleCount + 1);
        for (int index = 0; index <= StandardSampleCount; index++)
        {
            double x = minX + (maxX - minX) * index / StandardSampleCount;
            double y = RailSurfaceService.RailSurfaceFun(x);
            if (double.IsNaN(y) || double.IsInfinity(y))
            {
                continue;
            }

            standardPoints.Add(new RailProfilePoint(x, y));
        }

        return standardPoints;
    }

    private static RepresentativeProfileBounds BuildBounds(
        IReadOnlyList<RailProfilePoint> representativePoints,
        IReadOnlyList<RailProfilePoint> standardPoints)
    {
        IEnumerable<RailProfilePoint> allPoints = representativePoints.Concat(standardPoints);
        double minX = allPoints.Min(point => point.X);
        double maxX = allPoints.Max(point => point.X);
        double minY = allPoints.Min(point => point.Y);
        double maxY = allPoints.Max(point => point.Y);

        double xMargin = Math.Max((maxX - minX) * 0.08, 1.0);
        double yMargin = Math.Max((maxY - minY) * 0.08, 1.0);

        return new RepresentativeProfileBounds(minX - xMargin, maxX + xMargin, minY - yMargin, maxY + yMargin);
    }
}

/// <summary>
/// 对比展示静态数据。
/// </summary>
/// <param name="AlignedRepresentativePoints">对齐后的代表点。</param>
/// <param name="StandardPoints">对齐后的标准轨面采样点。</param>
/// <param name="Bounds">绘图边界。</param>
/// <param name="PointCountText">代表点数量文本。</param>
/// <param name="XRangeText">X 范围文本。</param>
/// <param name="YRangeText">Y 范围文本。</param>
public sealed record RepresentativeProfileComparisonSnapshot(
    IReadOnlyList<RailProfilePoint> AlignedRepresentativePoints,
    IReadOnlyList<IReadOnlyList<RailProfilePoint>> AlignedRepresentativeSegments,
    IReadOnlyList<RailProfilePoint> StandardPoints,
    RepresentativeProfileBounds Bounds,
    string PointCountText,
    string XRangeText,
    string YRangeText);

/// <summary>
/// Left/Right 最大掉块廓形的同坐标系展示快照。
/// </summary>
public sealed record MaximumDropProfileComparisonSnapshot(
    IReadOnlyList<RailProfilePoint> AlignedLeftPoints,
    IReadOnlyList<IReadOnlyList<RailProfilePoint>> AlignedLeftSegments,
    IReadOnlyList<RailProfilePoint> AlignedRightPoints,
    IReadOnlyList<IReadOnlyList<RailProfilePoint>> AlignedRightSegments,
    IReadOnlyList<RailProfilePoint> StandardPoints,
    RepresentativeProfileBounds Bounds,
    string PointCountText,
    string XRangeText,
    string YRangeText);

/// <summary>
/// 绘图边界。
/// </summary>
/// <param name="MinX">X 最小值。</param>
/// <param name="MaxX">X 最大值。</param>
/// <param name="MinY">Y 最小值。</param>
/// <param name="MaxY">Y 最大值。</param>
public readonly record struct RepresentativeProfileBounds(double MinX, double MaxX, double MinY, double MaxY);

/// <summary>
/// 屏幕采样点（左上角坐标）。
/// </summary>
/// <param name="Left">屏幕左坐标。</param>
/// <param name="Top">屏幕上坐标。</param>
public readonly record struct RepresentativeProfileScreenPoint(double Left, double Top);

/// <summary>
/// 绘制结果。
/// </summary>
/// <param name="RepresentativeCurveGeometry">代表轨面曲线几何。</param>
/// <param name="StandardCurveGeometry">标准轨面曲线几何。</param>
/// <param name="RepresentativePoints">代表点屏幕坐标。</param>
/// <param name="XAxisX1">X 轴起点 X。</param>
/// <param name="XAxisX2">X 轴终点 X。</param>
/// <param name="XAxisY1">X 轴起点 Y。</param>
/// <param name="XAxisY2">X 轴终点 Y。</param>
/// <param name="YAxisX1">Y 轴起点 X。</param>
/// <param name="YAxisX2">Y 轴终点 X。</param>
/// <param name="YAxisY1">Y 轴起点 Y。</param>
/// <param name="YAxisY2">Y 轴终点 Y。</param>
public sealed record RepresentativeProfilePlotResult(
    Geometry RepresentativeCurveGeometry,
    Geometry StandardCurveGeometry,
    IReadOnlyList<RepresentativeProfileScreenPoint> RepresentativePoints,
    double XAxisX1,
    double XAxisX2,
    double XAxisY1,
    double XAxisY2,
    double YAxisX1,
    double YAxisX2,
    double YAxisY1,
    double YAxisY2)
{
    public static RepresentativeProfilePlotResult Empty { get; } = new(
        Geometry.Empty,
        Geometry.Empty,
        Array.Empty<RepresentativeProfileScreenPoint>(),
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0);
}

/// <summary>
/// Left/Right 最大掉块廓形的绘制结果。
/// </summary>
public sealed record MaximumDropProfilePlotResult(
    Geometry LeftCurveGeometry,
    Geometry RightCurveGeometry,
    Geometry StandardCurveGeometry,
    IReadOnlyList<RepresentativeProfileScreenPoint> LeftPoints,
    IReadOnlyList<RepresentativeProfileScreenPoint> RightPoints,
    double XAxisX1,
    double XAxisX2,
    double XAxisY1,
    double XAxisY2,
    double YAxisX1,
    double YAxisX2,
    double YAxisY1,
    double YAxisY2)
{
    public static MaximumDropProfilePlotResult Empty { get; } = new(
        Geometry.Empty,
        Geometry.Empty,
        Geometry.Empty,
        Array.Empty<RepresentativeProfileScreenPoint>(),
        Array.Empty<RepresentativeProfileScreenPoint>(),
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0);
}
