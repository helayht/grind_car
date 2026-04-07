using System;
using System.Collections.Generic;
using System.Globalization;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail;

namespace GrindCar.Services;

public class RailSurfaceService
{
    private const double StraightAngleDegrees = 180.0;
    private const double DegreesToRadiansFactor = Math.PI / 180.0;

    private static readonly Arc[] Arcs =
    {
        new Arc{ XMin=-35.4, XMax=-25.3, LeftClosed=true,  RightClosed=false, R=13,  C=-22.42, D=161.15 },
        new Arc{ XMin=-25.3, XMax=-10,   LeftClosed=true,  RightClosed=false, R=80,  C=-7.3,   D=95.88  },
        new Arc{ XMin=-10,   XMax=10,    LeftClosed=true,  RightClosed=true,  R=300, C=0,      D=-124   },
        new Arc{ XMin=10,    XMax=25.35, LeftClosed=false, RightClosed=true,  R=80,  C=7.3,    D=95.88  },
        new Arc{ XMin=25.35, XMax=35.4,  LeftClosed=false, RightClosed=true,  R=13,  C=22.42,  D=161.15 }
    };

    /**
     * 轨面廓形标准计算函数
     */
    public static double RailSurfaceFun(double x)
    {
        foreach (var a in Arcs)
        {
            if (!a.Contains(x)) continue;
            double v = a.R * a.R - Math.Pow(x - a.C, 2);
            if (v < 0) v = 0;
            return Math.Sqrt(v) + a.D;
        }
        return double.NaN; // 默认情况，x超出范围
    }

    private struct Arc
    {
        public double XMin, XMax;
        public bool LeftClosed, RightClosed;
        public double R, C, D; // f(x)=sqrt(R^2-(x-C)^2)+D

        public bool Contains(double x)
        {
            bool leftOk = LeftClosed ? x >= XMin : x > XMin;
            bool rightOk = RightClosed ? x <= XMax : x < XMax;
            return leftOk && rightOk;
        }
    }

    public static double GetGrindDepth(int x)
    {
        IReadOnlyList<RailProfilePoint> representativeSectionPoints = GetRepresentativeSectionPoints();
        return GetGrindDepth(x, representativeSectionPoints);
    }

    public static IReadOnlyList<GrindDepthResult> GetGrindDepths(IReadOnlyList<int> angles)
    {
        if (angles == null)
        {
            throw new ArgumentNullException(nameof(angles));
        }

        if (angles.Count == 0)
        {
            return Array.Empty<GrindDepthResult>();
        }

        // 批量计算时只采集一次代表截面点集，避免每个角度都重复触发点云采集。
        IReadOnlyList<RailProfilePoint> representativeSectionPoints = GetRepresentativeSectionPoints();
        var results = new List<GrindDepthResult>(angles.Count);

        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            double grindDepth = GetGrindDepth(angle, representativeSectionPoints);
            results.Add(new GrindDepthResult(angle, grindDepth));
        }

        return results;
    }

    private static double GetGrindDepth(int angle, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        double k = CalculateSlopeFromAngle(angle);
        return Math.Abs(GetB(k, representativeSectionPoints) - SolveB(k));
    }

    private static double CalculateSlopeFromAngle(int angle)
    {
        // Math.Tan 接收弧度，因此需要先将角度转换为弧度。
        double radians = (StraightAngleDegrees + angle) * DegreesToRadiansFactor;
        return Math.Tan(radians);
    }

    /**
     * 获取代表截面的切点
     */
    public static double GetB(double k)
    {
        IReadOnlyList<RailProfilePoint> representativeSectionPoints = GetRepresentativeSectionPoints();
        return GetB(k, representativeSectionPoints);
    }

    private static IReadOnlyList<RailProfilePoint> GetRepresentativeSectionPoints()
    {
        IPointCloudMedianSectionCaptureService medianSectionCaptureService = new PointCloudMedianSectionCaptureService();
        PointCloudExportService pointCloudExportService = new PointCloudExportService();
        IReadOnlyList<PointCloudDeviceInfo> pointCloudDeviceInfos = pointCloudExportService.GetDevices();
        if (pointCloudDeviceInfos.Count == 0)
        {
            throw new PointCloudSdkException("未找到任何点云设备。");
        }

        var mergedPoints = new List<RailProfilePoint>();
        foreach (PointCloudDeviceInfo pointCloudDeviceInfo in pointCloudDeviceInfos)
        {
            PointCloudMedianSectionCaptureResult result =
                medianSectionCaptureService.CaptureMedianSectionProfile(pointCloudDeviceInfo.SerialNumber);

            if (result.ExtractionResult.ProfilePoints.Count == 0)
            {
                continue;
            }

            mergedPoints.AddRange(result.ExtractionResult.ProfilePoints);
        }

        if (mergedPoints.Count == 0)
        {
            throw new InvalidOperationException("未能从任何点云设备提取到有效的中位截面点。");
        }

        return mergedPoints;
    }

    private static double GetB(double k, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
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

        // 复制后再排序，避免修改调用方传入的点集顺序。
        var mergedPoints = new List<RailProfilePoint>(representativeSectionPoints);
        mergedPoints.Sort((a, b) => a.X.CompareTo(b.X));

        double xMid = (mergedPoints[0].X + mergedPoints[mergedPoints.Count - 1].X) / 2.0;
        double yMid = (mergedPoints[0].Y + mergedPoints[mergedPoints.Count - 1].Y) / 2.0;

        double b = double.NegativeInfinity;
        foreach (RailProfilePoint point in mergedPoints)
        {
            double candidate = (point.Y - yMid) - k * (point.X - xMid);
            if (candidate > b)
            {
                b = candidate;
            }
        }

        if (double.IsNegativeInfinity(b))
        {
            throw new InvalidOperationException(
                $"未能基于 {mergedPoints.Count.ToString(CultureInfo.InvariantCulture)} 个代表点计算有效的 b。");
        }

        return b;
    }

    /// <summary>
    /// 给定斜率 k，返回第一次接触时的最小 b，使得 y=kx+b 在轨面上方且刚好相切/接触
    /// </summary>
    public static double SolveB(double k)
    {
        return SolveBAndTouchPoint(k).b;
    }

    /// <summary>
    /// 返回 b 以及第一次接触点 x（方便你调试/验证）
    /// </summary>
    public static (double b, double xTouch) SolveBAndTouchPoint(double k)
    {
        if (double.IsNaN(k) || double.IsInfinity(k))
        {
            throw new ArgumentException("参数 k 必须为有限数值。", nameof(k));
        }

        // 候选点：所有闭合边界点
        var candidates = new List<double>();
        foreach (var a in Arcs)
        {
            if (a.LeftClosed) candidates.Add(a.XMin);
            if (a.RightClosed) candidates.Add(a.XMax);
        }

        // 内部切点：x_t = C - (kR)/sqrt(1+k^2)
        double s = Math.Sqrt(1.0 + k * k);
        foreach (var a in Arcs)
        {
            double xt = a.C - (k * a.R) / s;
            if (a.Contains(xt))
            {
                double fx = RailSurfaceFun(xt);
                if (!double.IsNaN(fx)) candidates.Add(xt);
            }
        }

        // 在候选点上取最大 b = f(x) - kx
        double bestB = double.NegativeInfinity;
        double bestX = double.NaN;

        foreach (double x in candidates)
        {
            double fx = RailSurfaceFun(x);
            if (double.IsNaN(fx)) continue;
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
}
