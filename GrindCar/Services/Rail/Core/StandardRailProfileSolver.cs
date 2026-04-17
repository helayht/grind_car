using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 标准轨面几何求解器。
/// </summary>
public static class StandardRailProfileSolver
{
    private static readonly Arc[] Arcs =
    {
        new(-35.4, -25.3, true, false, 13, -22.42, 161.15),
        new(-25.3, -10, true, false, 80, -7.3, 95.88),
        new(-10, 10, true, true, 300, 0, -124),
        new(10, 25.35, false, true, 80, 7.3, 95.88),
        new(25.35, 35.4, false, true, 13, 22.42, 161.15)
    };

    public static double RailSurfaceFun(double x)
    {
        for (int index = 0; index < Arcs.Length; index++)
        {
            Arc arc = Arcs[index];
            if (!arc.Contains(x))
            {
                continue;
            }

            double value = arc.R * arc.R - Math.Pow(x - arc.C, 2);
            if (value < 0)
            {
                value = 0;
            }

            return Math.Sqrt(value) + arc.D;
        }

        return double.NaN;
    }

    public static double SolveB(double k)
    {
        return SolveBAndTouchPoint(k).b;
    }

    public static (double b, double xTouch) SolveBAndTouchPoint(double k)
    {
        if (double.IsNaN(k) || double.IsInfinity(k))
        {
            throw new ArgumentException("参数 k 必须为有限数值。", nameof(k));
        }

        var candidates = new List<double>();
        for (int index = 0; index < Arcs.Length; index++)
        {
            Arc arc = Arcs[index];
            if (arc.LeftClosed)
            {
                candidates.Add(arc.XMin);
            }

            if (arc.RightClosed)
            {
                candidates.Add(arc.XMax);
            }
        }

        double denominator = Math.Sqrt(1.0 + k * k);
        for (int index = 0; index < Arcs.Length; index++)
        {
            Arc arc = Arcs[index];
            double xTouch = arc.C - (k * arc.R) / denominator;
            if (!arc.Contains(xTouch))
            {
                continue;
            }

            double fx = RailSurfaceFun(xTouch);
            if (!double.IsNaN(fx))
            {
                candidates.Add(xTouch);
            }
        }

        double bestB = double.NegativeInfinity;
        double bestX = double.NaN;

        for (int index = 0; index < candidates.Count; index++)
        {
            double x = candidates[index];
            double fx = RailSurfaceFun(x);
            if (double.IsNaN(fx))
            {
                continue;
            }

            double b = fx - k * x;
            if (b > bestB)
            {
                bestB = b;
                bestX = x;
            }
        }

        if (double.IsNegativeInfinity(bestB))
        {
            throw new InvalidOperationException("未找到有效接触点，请检查轨面函数与参数 k。");
        }

        return (bestB, bestX);
    }

    public static double GetRepresentativeB(double k, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        if (double.IsNaN(k) || double.IsInfinity(k))
        {
            throw new ArgumentException("参数 k 必须为有限数值。", nameof(k));
        }

        if (representativeSectionPoints == null)
        {
            throw new ArgumentNullException(nameof(representativeSectionPoints));
        }

        if (representativeSectionPoints.Count == 0)
        {
            throw new InvalidOperationException("代表截面点集不能为空。");
        }

        var sortedPoints = new List<RailProfilePoint>(representativeSectionPoints);
        sortedPoints.Sort((left, right) => left.X.CompareTo(right.X));

        double xMid = (sortedPoints[0].X + sortedPoints[^1].X) / 2.0;
        double yMid = (sortedPoints[0].Y + sortedPoints[^1].Y) / 2.0;

        double best = double.NegativeInfinity;
        for (int index = 0; index < sortedPoints.Count; index++)
        {
            RailProfilePoint point = sortedPoints[index];
            double candidate = (point.Y - yMid) - k * (point.X - xMid);
            if (candidate > best)
            {
                best = candidate;
            }
        }

        if (double.IsNegativeInfinity(best))
        {
            throw new InvalidOperationException("未能基于代表点计算有效的 b。 ");
        }

        return best;
    }

    private readonly struct Arc
    {
        public Arc(double xMin, double xMax, bool leftClosed, bool rightClosed, double r, double c, double d)
        {
            XMin = xMin;
            XMax = xMax;
            LeftClosed = leftClosed;
            RightClosed = rightClosed;
            R = r;
            C = c;
            D = d;
        }

        public double XMin { get; }

        public double XMax { get; }

        public bool LeftClosed { get; }

        public bool RightClosed { get; }

        public double R { get; }

        public double C { get; }

        public double D { get; }

        public bool Contains(double x)
        {
            bool leftOk = LeftClosed ? x >= XMin : x > XMin;
            bool rightOk = RightClosed ? x <= XMax : x < XMax;
            return leftOk && rightOk;
        }
    }
}