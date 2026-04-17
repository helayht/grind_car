using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GrindCar.Models.Rail;
using GrindCar.Services;

namespace GrindCar.Views;

/// <summary>
/// 代表轨面与标准轨面的曲线对比窗口。
/// </summary>
public partial class RepresentativeProfileComparisonWindow : Window
{
    private const double PlotPadding = 36.0;
    private const double PointDiameter = 5.0;
    private const int RepresentativeInterpolationSteps = 10;
    private const int StandardSampleCount = 240;

    private readonly IReadOnlyList<RailProfilePoint> _alignedRepresentativePoints;
    private readonly IReadOnlyList<RailProfilePoint> _standardPoints;
    private readonly Bounds _bounds;

    /// <summary>
    /// 初始化代表轨面与标准轨面对比窗口。
    /// </summary>
    /// <param name="representativePoints">待展示的代表截面点集。</param>
    public RepresentativeProfileComparisonWindow(IReadOnlyList<RailProfilePoint> representativePoints)
    {
        if (representativePoints == null)
        {
            throw new ArgumentNullException(nameof(representativePoints));
        }

        if (representativePoints.Count < 2)
        {
            throw new InvalidOperationException("代表点数量不足，无法绘制曲线。");
        }

        InitializeComponent();

        AlignmentResult alignmentResult = AlignRepresentativePoints(representativePoints);
        _alignedRepresentativePoints = alignmentResult.Points;
        _standardPoints = BuildStandardPoints(alignmentResult);
        _bounds = BuildBounds(_alignedRepresentativePoints, _standardPoints);

        UpdateSummary();
        Loaded += RepresentativeProfileComparisonWindow_Loaded;
    }

    /// <summary>
    /// 窗口加载完成后绘制对比曲线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">窗口加载事件参数。</param>
    private void RepresentativeProfileComparisonWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RenderPlot();
    }

    /// <summary>
    /// 在绘图区域尺寸变化时重新渲染曲线。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">尺寸变化事件参数。</param>
    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RenderPlot();
    }

    /// <summary>
    /// 关闭当前窗口。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 更新代表轨面和标准轨面的摘要统计信息。
    /// </summary>
    private void UpdateSummary()
    {
        PointCountTextBlock.Text = _alignedRepresentativePoints.Count.ToString(CultureInfo.InvariantCulture);
        IEnumerable<RailProfilePoint> allPoints = _alignedRepresentativePoints.Concat(_standardPoints);
        XRangeTextBlock.Text = $"{allPoints.Min(point => point.X):0.###} ~ {allPoints.Max(point => point.X):0.###}";
        YRangeTextBlock.Text = $"{allPoints.Min(point => point.Y):0.###} ~ {allPoints.Max(point => point.Y):0.###}";
    }

    /// <summary>
    /// 根据当前窗口尺寸重新绘制坐标轴和两条轨面曲线。
    /// </summary>
    private void RenderPlot()
    {
        if (PlotCanvas.ActualWidth <= PlotPadding * 2.0 || PlotCanvas.ActualHeight <= PlotPadding * 2.0)
        {
            return;
        }

        IReadOnlyList<Point> representativeScreenPoints = MapToScreen(_alignedRepresentativePoints);
        IReadOnlyList<Point> standardScreenPoints = MapToScreen(_standardPoints);

        RepresentativeCurvePath.Data = BuildSmoothGeometry(representativeScreenPoints);
        StandardCurvePath.Data = BuildPolylineGeometry(standardScreenPoints);
        DrawRepresentativePoints(representativeScreenPoints);
        DrawAxes();
    }

    /// <summary>
    /// 在画布上绘制代表轨面的采样点。
    /// </summary>
    /// <param name="points">已经映射到屏幕坐标系的点集合。</param>
    private void DrawRepresentativePoints(IReadOnlyList<Point> points)
    {
        RepresentativePointCanvas.Children.Clear();

        for (int index = 0; index < points.Count; index++)
        {
            Point point = points[index];
            var ellipse = new Ellipse
            {
                Width = PointDiameter,
                Height = PointDiameter,
                Fill = (Brush)FindResource("PointBrush")
            };

            Canvas.SetLeft(ellipse, point.X - PointDiameter / 2.0);
            Canvas.SetTop(ellipse, point.Y - PointDiameter / 2.0);
            RepresentativePointCanvas.Children.Add(ellipse);
        }
    }

    /// <summary>
    /// 根据零点位置绘制 X 轴和 Y 轴。
    /// </summary>
    private void DrawAxes()
    {
        double xAxisY = MapY(0.0);
        double yAxisX = MapX(0.0);

        XAxis.X1 = PlotPadding;
        XAxis.X2 = PlotCanvas.ActualWidth - PlotPadding;
        XAxis.Y1 = xAxisY;
        XAxis.Y2 = xAxisY;

        YAxis.X1 = yAxisX;
        YAxis.X2 = yAxisX;
        YAxis.Y1 = PlotPadding;
        YAxis.Y2 = PlotCanvas.ActualHeight - PlotPadding;
    }

    /// <summary>
    /// 将轨面坐标点映射到画布屏幕坐标。
    /// </summary>
    /// <param name="points">轨面坐标点集合。</param>
    /// <returns>对应的屏幕坐标点集合。</returns>
    private IReadOnlyList<Point> MapToScreen(IReadOnlyList<RailProfilePoint> points)
    {
        return points.Select(point => new Point(MapX(point.X), MapY(point.Y))).ToArray();
    }

    /// <summary>
    /// 将轨面横向坐标映射为画布 X 坐标。
    /// </summary>
    /// <param name="x">轨面横向坐标。</param>
    /// <returns>对应的画布 X 坐标。</returns>
    private double MapX(double x)
    {
        double width = PlotCanvas.ActualWidth - PlotPadding * 2.0;
        double xRange = _bounds.MaxX - _bounds.MinX;
        double safeRange = xRange <= 0.0 ? 1.0 : xRange;
        return PlotPadding + (x - _bounds.MinX) / safeRange * width;
    }

    /// <summary>
    /// 将轨面高度坐标映射为画布 Y 坐标。
    /// </summary>
    /// <param name="y">轨面高度坐标。</param>
    /// <returns>对应的画布 Y 坐标。</returns>
    private double MapY(double y)
    {
        double height = PlotCanvas.ActualHeight - PlotPadding * 2.0;
        double yRange = _bounds.MaxY - _bounds.MinY;
        double safeRange = yRange <= 0.0 ? 1.0 : yRange;
        return PlotPadding + (_bounds.MaxY - y) / safeRange * height;
    }

    /// <summary>
    /// 使用折线方式构建几何路径。
    /// </summary>
    /// <param name="points">屏幕坐标点集合。</param>
    /// <returns>可用于 WPF Path 的折线几何对象。</returns>
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

    /// <summary>
    /// 使用平滑插值构建代表轨面的几何路径。
    /// </summary>
    /// <param name="points">屏幕坐标点集合。</param>
    /// <returns>平滑后的几何对象；当点数不足时退化为折线。</returns>
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

    /// <summary>
    /// 基于 Catmull-Rom 插值生成平滑过渡点集合。
    /// </summary>
    /// <param name="points">原始屏幕坐标点集合。</param>
    /// <returns>插值后的点序列。</returns>
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

    /// <summary>
    /// 计算 Catmull-Rom 曲线上参数 <paramref name="t"/> 处的插值点。
    /// </summary>
    /// <param name="p0">前一个控制点。</param>
    /// <param name="p1">当前控制点。</param>
    /// <param name="p2">下一个控制点。</param>
    /// <param name="p3">后一个控制点。</param>
    /// <param name="t">插值参数，范围通常为 0 到 1。</param>
    /// <returns>插值得到的曲线点。</returns>
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

    /// <summary>
    /// 将代表截面点集按中点平移到局部坐标系，便于与标准轨面对齐。
    /// </summary>
    /// <param name="representativePoints">原始代表截面点集。</param>
    /// <returns>包含平移后点集及中心点信息的对齐结果。</returns>
    private static AlignmentResult AlignRepresentativePoints(IReadOnlyList<RailProfilePoint> representativePoints)
    {
        List<RailProfilePoint> sortedPoints = representativePoints
            .OrderBy(point => point.X)
            .ToList();

        double xMid = (sortedPoints[0].X + sortedPoints[sortedPoints.Count - 1].X) / 2.0;
        double yMid = (sortedPoints[0].Y + sortedPoints[sortedPoints.Count - 1].Y) / 2.0;

        RailProfilePoint[] alignedPoints = sortedPoints
            .Select(point => new RailProfilePoint(point.X - xMid, point.Y - yMid))
            .ToArray();

        return new AlignmentResult(alignedPoints, xMid, yMid);
    }

    /// <summary>
    /// 基于对齐结果采样标准轨面曲线点集。
    /// </summary>
    /// <param name="alignmentResult">代表轨面点的对齐结果。</param>
    /// <returns>与代表轨面对齐后的标准轨面点集合。</returns>
    private static IReadOnlyList<RailProfilePoint> BuildStandardPoints(AlignmentResult alignmentResult)
    {
        IReadOnlyList<RailProfilePoint> alignedRepresentativePoints = alignmentResult.Points;
        double minX = alignedRepresentativePoints[0].X;
        double maxX = alignedRepresentativePoints[alignedRepresentativePoints.Count - 1].X;

        var standardPoints = new List<RailProfilePoint>(StandardSampleCount + 1);
        for (int index = 0; index <= StandardSampleCount; index++)
        {
            double normalizedX = minX + (maxX - minX) * index / StandardSampleCount;
            double originalX = normalizedX + alignmentResult.XMid;
            double originalY = RailSurfaceService.RailSurfaceFun(originalX);
            if (double.IsNaN(originalY) || double.IsInfinity(originalY))
            {
                continue;
            }
            standardPoints.Add(new RailProfilePoint(normalizedX, originalY - alignmentResult.YMid));
        }
        return standardPoints;
    }

    /// <summary>
    /// 根据两组点计算绘图所需的包围盒范围。
    /// </summary>
    /// <param name="representativePoints">代表轨面点集。</param>
    /// <param name="standardPoints">标准轨面点集。</param>
    /// <returns>包含边界与留白后的绘图范围。</returns>
    private static Bounds BuildBounds(IReadOnlyList<RailProfilePoint> representativePoints, IReadOnlyList<RailProfilePoint> standardPoints)
    {
        IEnumerable<RailProfilePoint> allPoints = representativePoints.Concat(standardPoints);

        double minX = allPoints.Min(point => point.X);
        double maxX = allPoints.Max(point => point.X);
        double minY = allPoints.Min(point => point.Y);
        double maxY = allPoints.Max(point => point.Y);

        double xMargin = Math.Max((maxX - minX) * 0.08, 1.0);
        double yMargin = Math.Max((maxY - minY) * 0.08, 1.0);

        return new Bounds(minX - xMargin, maxX + xMargin, minY - yMargin, maxY + yMargin);
    }

    private readonly record struct Bounds(double MinX, double MaxX, double MinY, double MaxY);
    private readonly record struct AlignmentResult(IReadOnlyList<RailProfilePoint> Points, double XMid, double YMid);
}
