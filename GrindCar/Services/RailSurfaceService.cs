using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail;

namespace GrindCar.Services;

public class RailSurfaceService
{
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
            bool leftOk  = LeftClosed  ? x >= XMin : x > XMin;
            bool rightOk = RightClosed ? x <= XMax : x < XMax;
            return leftOk && rightOk;
        }
    }

    /**
     * 获取代表截面的切点
     */
    public static double GetB(double k)
    {
        IPointCloudMedianSectionCaptureService _medianSectionCaptureService = new PointCloudMedianSectionCaptureService();
        PointCloudExportService _pointCloudExportService = new PointCloudExportService();
        var pointCloudDeviceInfos = _pointCloudExportService.GetDevices();
        if (pointCloudDeviceInfos.Count == 0)
        {
            throw new PointCloudSdkException("未找到任何点云设备。");
        }
        PointCloudMedianSectionCaptureResult result = _medianSectionCaptureService.CaptureMedianSectionProfile("");
        var points = result.ExtractionResult.ProfilePoints;
        double b = -0x3f3f3f;
        for (var i = 0; i < points.Count; i++)
        {
            b = Math.Max(b, points[i].Y - k * points[i].X);
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
