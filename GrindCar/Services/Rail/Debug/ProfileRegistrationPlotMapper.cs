using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 代表廓形配准窗口等比例坐标映射。
/// </summary>
internal sealed class ProfileRegistrationPlotMapper
{
    private readonly double _plotPadding;
    private double _plotWidth;
    private double _plotHeight;
    private PlotBounds _bounds;
    private double _scale;
    private double _originX;
    private double _originY;

    public ProfileRegistrationPlotMapper(double plotPadding)
    {
        _plotPadding = plotPadding;
    }

    public double Scale => _scale;

    public void Update(IReadOnlyList<RailProfilePoint> points, double plotWidth, double plotHeight)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            _plotWidth = plotWidth;
            _plotHeight = plotHeight;
            _bounds = new PlotBounds(0.0, 1.0, 0.0, 1.0);
            _scale = 1.0;
            _originX = _plotPadding;
            _originY = _plotPadding;
            return;
        }

        _plotWidth = plotWidth;
        _plotHeight = plotHeight;
        _bounds = BuildBounds(points);

        double availableWidth = Math.Max(plotWidth - _plotPadding * 2.0, 1.0);
        double availableHeight = Math.Max(plotHeight - _plotPadding * 2.0, 1.0);
        double xRange = Math.Max(_bounds.MaxX - _bounds.MinX, 1.0);
        double yRange = Math.Max(_bounds.MaxY - _bounds.MinY, 1.0);
        _scale = Math.Min(availableWidth / xRange, availableHeight / yRange);

        double usedWidth = xRange * _scale;
        double usedHeight = yRange * _scale;
        _originX = _plotPadding + (availableWidth - usedWidth) / 2.0;
        _originY = _plotPadding + (availableHeight - usedHeight) / 2.0;
    }

    public IReadOnlyList<Point> MapToScreen(IReadOnlyList<RailProfilePoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        return points.Select(point => MapToScreen(point)).ToArray();
    }

    public Point MapToScreen(RailProfilePoint point)
    {
        double x = _originX + (point.X - _bounds.MinX) * _scale;
        double y = _originY + (_bounds.MaxY - point.Y) * _scale;
        return new Point(x, y);
    }

    public double MapX(double x)
    {
        return MapToScreen(new RailProfilePoint(x, 0.0)).X;
    }

    public double MapY(double y)
    {
        return MapToScreen(new RailProfilePoint(0.0, y)).Y;
    }

    private static PlotBounds BuildBounds(IReadOnlyList<RailProfilePoint> points)
    {
        double minX = points.Min(point => point.X);
        double maxX = points.Max(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxY = points.Max(point => point.Y);
        double xMargin = Math.Max((maxX - minX) * 0.08, 1.0);
        double yMargin = Math.Max((maxY - minY) * 0.08, 1.0);
        return new PlotBounds(minX - xMargin, maxX + xMargin, minY - yMargin, maxY + yMargin);
    }

    private readonly record struct PlotBounds(double MinX, double MaxX, double MinY, double MaxY);
}
