using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 标准轨面几何求解器。
/// </summary>
public static class StandardRailProfileSolver
{
    public const double LeftBoundaryX = -35.4;
    public const double RightBoundaryX = 35.4;

    private const double StandardProfileVerticalOffset = -176.0;
    private const int SlopeCacheDigits = 12;

    private static readonly Arc[] Arcs60Kg =
    {
        new(-35.4, -25.3, true, false, 13, -22.42, 161.15),
        new(-25.3, -10, true, false, 80, -7.3, 95.88),
        new(-10, 10, true, true, 300, 0, -124),
        new(10, 25.35, false, true, 80, 7.3, 95.88),
        new(25.35, 35.4, false, true, 13, 22.42, 161.15)
    };

    /// <summary>
    /// 50kg/m 高精度分段圆弧参数。
    /// 三段相切圆弧，外侧边界延伸至 ±35.4。
    /// </summary>
    private static readonly Arc[] Arcs50Kg =
    {
        new(-35.4, -23, true, false, 13, -22, 138.16),
        new(-23, 23, true, true, 300, 0, -148),
        new(23, 35.4, false, true, 13, 22, 138.16)
    };

    private static Arc[] _activeArcs = Arcs60Kg;

    private static readonly ConcurrentDictionary<double, TangentSolution> TangentSolutionCache = new();

    public static double RailSurfaceFun(double x)
    {
        for (int index = 0; index < _activeArcs.Length; index++)
        {
            Arc arc = _activeArcs[index];
            if (!arc.Contains(x))
            {
                continue;
            }

            double value = arc.R * arc.R - Math.Pow(x - arc.C, 2);
            if (value < 0)
            {
                value = 0;
            }

            return Math.Sqrt(value) + arc.D + StandardProfileVerticalOffset;
        }

        return double.NaN;
    }

    public static double SolveB(double k)
    {
        return GetOrAddTangentSolution(k).B;
    }

    public static (double b, double xTouch) SolveBAndTouchPoint(double k)
    {
        TangentSolution solution = GetOrAddTangentSolution(k);
        return (solution.B, solution.XTouch);
    }

    private static TangentSolution GetOrAddTangentSolution(double k)
    {
        if (double.IsNaN(k) || double.IsInfinity(k))
        {
            throw new ArgumentException("参数 k 必须为有限数值。", nameof(k));
        }

        double normalizedK = NormalizeSlope(k);
        return TangentSolutionCache.GetOrAdd(normalizedK, ComputeTangentSolution);
    }

    private static TangentSolution ComputeTangentSolution(double k)
    {
        var candidates = new List<double>();
        for (int index = 0; index < _activeArcs.Length; index++)
        {
            Arc arc = _activeArcs[index];
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
        for (int index = 0; index < _activeArcs.Length; index++)
        {
            Arc arc = _activeArcs[index];
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

        return new TangentSolution(bestB, bestX);
    }

    private static double NormalizeSlope(double k)
    {
        return Math.Round(k, SlopeCacheDigits, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 当前激活的轨面型号。
    /// </summary>
    public static RailProfileType CurrentProfileType { get; private set; } = RailProfileType.Kg60;

    /// <summary>
    /// 切换轨面型号并清空切线求解缓存。
    /// </summary>
    public static void SwitchProfile(RailProfileType type)
    {
        if (CurrentProfileType == type)
        {
            return;
        }

        _activeArcs = type == RailProfileType.Kg50 ? Arcs50Kg : Arcs60Kg;
        CurrentProfileType = type;
        TangentSolutionCache.Clear();
    }

    /// <summary>
    /// 计算代表截面点集在给定斜率 k 下的截距 b。
    /// 取前 0.5% 最大候选值的平均值代替全局最大值，以抵御传感器飞点等异常值的干扰。
    /// 物理含义：直线 y = kx + b 从上方下降时首次"托住"代表廓形的位置。
    /// </summary>
    /// <param name="k">斜率 k = tan(角度)。</param>
    /// <param name="representativeSectionPoints">代表廓形二维点集。</param>
    /// <returns>代表截距 b。</returns>
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

        int count = representativeSectionPoints.Count;
        double[] candidates = new double[count];
        for (int index = 0; index < count; index++)
        {
            RailProfilePoint point = representativeSectionPoints[index];
            candidates[index] = point.Y - k * point.X;
        }

        // 取前 0.5% 最大值的平均，对抗单个飞点的干扰。
        // 至少保留 1 个点，避免小数截断导致 topCount = 0。
        int topCount = Math.Max(1, count / 200);
        if (topCount == 1)
        {
            // 快速路径：点数较少时退化为取最大值，性能最优
            double best = double.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                if (candidates[index] > best)
                {
                    best = candidates[index];
                }
            }

            if (double.IsNegativeInfinity(best))
            {
                throw new InvalidOperationException("未能基于代表点计算有效的 b。");
            }

            return best;
        }

        // 用 QuickSelect 找到第 (count - topCount) 小的元素，
        // 则该元素及之后的所有元素即为前 topCount 个最大值。
        int thresholdIndex = count - topCount;
        double threshold = QuickSelect.SelectKthSmallest(candidates, thresholdIndex);

        double sum = 0.0;
        int included = 0;
        for (int index = 0; index < count; index++)
        {
            if (candidates[index] >= threshold)
            {
                sum += candidates[index];
                included++;
                if (included >= topCount)
                {
                    break;
                }
            }
        }

        return sum / included;
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

    private readonly record struct TangentSolution(double B, double XTouch);
}
