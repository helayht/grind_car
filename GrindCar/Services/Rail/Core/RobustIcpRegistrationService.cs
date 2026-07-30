using System;
using System.Collections.Generic;
using System.Linq;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Core;

/// <summary>
/// 稳健 ICP 二维精对齐服务，自带截断逻辑以抵抗局部噪点。
/// </summary>
public sealed class RobustIcpRegistrationService
{
    private const int MaxIterations = 30;
    private const double Tolerance = 1e-4;
    internal const double InlierRatio = 0.8;

    /// <summary>
    /// 对给定点云进行精对齐，返回相对于输入的附加变换参数 (dDx, dDy, dRotation)。
    /// </summary>
    public ProfileRegistrationParameters Align(
        IReadOnlyList<RailProfilePoint> sourcePoints,
        IReadOnlyList<RailProfilePoint> targetPoints)
    {
        if (sourcePoints.Count == 0 || targetPoints.Count == 0)
        {
            return new ProfileRegistrationParameters(0, 0, 0);
        }

        double accumulatedDx = 0.0;
        double accumulatedDy = 0.0;
        double accumulatedRotationRadians = 0.0;

        List<RailProfilePoint> currentSource = sourcePoints.ToList();

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // 1. 找最近点对并计算距离
            var matches = new List<(RailProfilePoint source, RailProfilePoint target, double distance)>();
            foreach (var sp in currentSource)
            {
                var tp = FindNearest(sp, targetPoints);
                double dist = Math.Sqrt(Math.Pow(sp.X - tp.X, 2) + Math.Pow(sp.Y - tp.Y, 2));
                matches.Add((sp, tp, dist));
            }

            // 2. 稳健截断 (Trim)：按距离排序，只保留前 InlierRatio 的内点
            matches = matches.OrderBy(m => m.distance).ToList();
            int inlierCount = ResolveInlierCount(matches.Count);
            var inliers = matches.Take(inlierCount).ToList();

            // 3. SVD 解算 2D 刚体变换
            (double dx, double dy, double dTheta) = CalculateRigidTransform(
                inliers.Select(m => m.source).ToList(),
                inliers.Select(m => m.target).ToList());

            (accumulatedDx, accumulatedDy, accumulatedRotationRadians) = ComposeRigidTransforms(
                accumulatedDx,
                accumulatedDy,
                accumulatedRotationRadians,
                dx,
                dy,
                dTheta);

            // 4. 应用当次变换，准备下一次迭代
            double cosVal = Math.Cos(dTheta);
            double sinVal = Math.Sin(dTheta);
            for (int i = 0; i < currentSource.Count; i++)
            {
                var p = currentSource[i];
                double nx = p.X * cosVal - p.Y * sinVal + dx;
                double ny = p.X * sinVal + p.Y * cosVal + dy;
                currentSource[i] = new RailProfilePoint(nx, ny);
            }

            // 5. 检查收敛
            if (Math.Abs(dx) < Tolerance && Math.Abs(dy) < Tolerance && Math.Abs(dTheta) < Tolerance)
            {
                break;
            }
        }

        double degrees = accumulatedRotationRadians * 180.0 / Math.PI;
        return new ProfileRegistrationParameters(accumulatedDx, accumulatedDy, degrees, false);
    }

    internal static int ResolveInlierCount(int pointCount)
    {
        if (pointCount <= 0)
        {
            return 0;
        }

        int requestedCount = Math.Max(3, (int)(pointCount * InlierRatio));
        return Math.Min(pointCount, requestedCount);
    }

    internal static (double dx, double dy, double rotationRadians) ComposeRigidTransforms(
        double accumulatedDx,
        double accumulatedDy,
        double accumulatedRotationRadians,
        double incrementalDx,
        double incrementalDy,
        double incrementalRotationRadians)
    {
        double incrementalCosValue = Math.Cos(incrementalRotationRadians);
        double incrementalSinValue = Math.Sin(incrementalRotationRadians);
        double composedDx =
            accumulatedDx * incrementalCosValue - accumulatedDy * incrementalSinValue + incrementalDx;
        double composedDy =
            accumulatedDx * incrementalSinValue + accumulatedDy * incrementalCosValue + incrementalDy;

        return (
            composedDx,
            composedDy,
            accumulatedRotationRadians + incrementalRotationRadians);
    }

    private static RailProfilePoint FindNearest(RailProfilePoint p, IReadOnlyList<RailProfilePoint> targets)
    {
        RailProfilePoint nearest = targets[0];
        double minDistSq = double.MaxValue;
        foreach (var t in targets)
        {
            double distSq = (p.X - t.X) * (p.X - t.X) + (p.Y - t.Y) * (p.Y - t.Y);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                nearest = t;
            }
        }
        return nearest;
    }

    private static (double dx, double dy, double dTheta) CalculateRigidTransform(
        List<RailProfilePoint> source,
        List<RailProfilePoint> target)
    {
        double centroidSourceX = source.Average(p => p.X);
        double centroidSourceY = source.Average(p => p.Y);
        double centroidTargetX = target.Average(p => p.X);
        double centroidTargetY = target.Average(p => p.Y);

        double sxx = 0, sxy = 0, syx = 0, syy = 0;
        for (int i = 0; i < source.Count; i++)
        {
            double sx = source[i].X - centroidSourceX;
            double sy = source[i].Y - centroidSourceY;
            double tx = target[i].X - centroidTargetX;
            double ty = target[i].Y - centroidTargetY;

            sxx += sx * tx;
            sxy += sx * ty;
            syx += sy * tx;
            syy += sy * ty;
        }

        // 解算旋转角
        double dTheta = Math.Atan2(sxy - syx, sxx + syy);

        // 解算平移量
        double cosVal = Math.Cos(dTheta);
        double sinVal = Math.Sin(dTheta);
        double dx = centroidTargetX - (centroidSourceX * cosVal - centroidSourceY * sinVal);
        double dy = centroidTargetY - (centroidSourceX * sinVal + centroidSourceY * cosVal);

        return (dx, dy, dTheta);
    }
}
