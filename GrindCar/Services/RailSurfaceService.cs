using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail;

namespace GrindCar.Services;

/// <summary>
/// 提供标准轨面函数、代表截面采集与打磨深度计算能力。
/// </summary>
public class RailSurfaceService
{
    private const double StraightAngleDegrees = 180.0;
    private const double DegreesToRadiansFactor = Math.PI / 180.0;
    private const string DefaultLogDirectoryName = "Log";
    private const string DefaultBaselineFileName = "grind-depth-baseline.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Arc[] Arcs =
    {
        new Arc{ XMin=-35.4, XMax=-25.3, LeftClosed=true,  RightClosed=false, R=13,  C=-22.42, D=161.15 },
        new Arc{ XMin=-25.3, XMax=-10,   LeftClosed=true,  RightClosed=false, R=80,  C=-7.3,   D=95.88  },
        new Arc{ XMin=-10,   XMax=10,    LeftClosed=true,  RightClosed=true,  R=300, C=0,      D=-124   },
        new Arc{ XMin=10,    XMax=25.35, LeftClosed=false, RightClosed=true,  R=80,  C=7.3,    D=95.88  },
        new Arc{ XMin=25.35, XMax=35.4,  LeftClosed=false, RightClosed=true,  R=13,  C=22.42,  D=161.15 }
    };

    /// <summary>
    /// 根据给定横向坐标计算标准轨面函数值。
    /// </summary>
    /// <param name="x">轨面横向坐标。</param>
    /// <returns>标准轨面在该横向位置上的高度；若超出定义域则返回 <see cref="double.NaN"/>。</returns>
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

        /// <summary>
        /// 判断给定横向坐标是否落在当前圆弧定义区间内。
        /// </summary>
        /// <param name="x">待判断的横向坐标。</param>
        /// <returns>若坐标落在圆弧有效区间内则返回 <c>true</c>。</returns>
        public bool Contains(double x)
        {
            bool leftOk = LeftClosed ? x >= XMin : x > XMin;
            bool rightOk = RightClosed ? x <= XMax : x < XMax;
            return leftOk && rightOk;
        }
    }
    
    /// <summary>
    /// 批量计算打磨深度，并返回过程中使用的代表截面点集。
    /// </summary>
    /// <param name="angles">待计算的角度集合。</param>
    /// <returns>包含计算结果和代表截面点集的对象。</returns>
    public static GrindDepthCalculationResult CalculateGrindDepths(IReadOnlyList<int> angles)
    {
        if (angles == null)
        {
            throw new ArgumentNullException(nameof(angles));
        }

        if (angles.Count == 0)
        {
            return new GrindDepthCalculationResult(Array.Empty<GrindDepthResult>(), Array.Empty<RailProfilePoint>());
        }

        // 批量计算时只采集一次代表截面点集，避免每个角度都重复触发点云采集。
        IReadOnlyList<RailProfilePoint> representativeSectionPoints = CaptureRepresentativeSectionPoints();
        var results = new List<GrindDepthResult>(angles.Count);

        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            double grindDepth = GetGrindDepth(angle, representativeSectionPoints);
            results.Add(new GrindDepthResult(angle, grindDepth));
        }

        return new GrindDepthCalculationResult(results, representativeSectionPoints);
    }

    /// <summary>
    /// 基于给定代表截面点集批量计算各角度对应的 b 值。
    /// </summary>
    /// <param name="angles">待计算的角度集合。</param>
    /// <param name="representativeSectionPoints">代表截面点集。</param>
    /// <returns>每个角度对应的 b 值结果集合。</returns>
    public static IReadOnlyList<AngleBValue> CalculateBValues(
        IReadOnlyList<int> angles,
        IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        if (angles == null)
        {
            throw new ArgumentNullException(nameof(angles));
        }

        if (representativeSectionPoints == null)
        {
            throw new ArgumentNullException(nameof(representativeSectionPoints));
        }

        var results = new List<AngleBValue>(angles.Count);
        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            double b = CalculateBByAngle(angle, representativeSectionPoints);
            results.Add(new AngleBValue(angle, b));
        }

        return results;
    }

    /// <summary>
    /// 基于指定代表廓形点集生成检测基线。
    /// </summary>
    /// <param name="angles">待计算的角度集合。</param>
    /// <param name="representativePoints">本次计算使用的代表廓形点集。</param>
    /// <returns>检测基线对象。</returns>
    public static GrindingDepthBaseline CreateGrindingDepthBaseline(
        IReadOnlyList<int> angles,
        IReadOnlyList<RailProfilePoint> representativePoints)
    {
        if (angles == null)
        {
            throw new ArgumentNullException(nameof(angles));
        }

        if (angles.Count == 0)
        {
            throw new InvalidOperationException("角度列表不能为空。");
        }

        if (representativePoints == null)
        {
            throw new ArgumentNullException(nameof(representativePoints));
        }

        IReadOnlyList<AngleBValue> baselineBValues = CalculateBValues(angles, representativePoints);
        return new GrindingDepthBaseline(DateTime.Now, angles.ToArray(), baselineBValues);
    }

    /// <summary>
    /// 保存最新检测基线数据。
    /// </summary>
    /// <param name="baseline">待保存的基线数据。</param>
    /// <param name="baselineFilePath">可选的基线文件路径。</param>
    public static void SaveGrindingDepthBaseline(GrindingDepthBaseline baseline, string? baselineFilePath = null)
    {
        if (baseline == null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        string resolvedPath = ResolveBaselineFilePath(baselineFilePath);
        string? directoryPath = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("基线文件目录无效。");
        }

        Directory.CreateDirectory(directoryPath);
        string json = JsonSerializer.Serialize(baseline, SerializerOptions);
        File.WriteAllText(resolvedPath, json);
    }

    /// <summary>
    /// 加载最新检测基线数据。
    /// </summary>
    /// <param name="baselineFilePath">可选的基线文件路径。</param>
    /// <returns>若基线存在则返回对应对象，否则返回 <c>null</c>。</returns>
    public static GrindingDepthBaseline? LoadLatestGrindingDepthBaseline(string? baselineFilePath = null)
    {
        string resolvedPath = ResolveBaselineFilePath(baselineFilePath);
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        string json = File.ReadAllText(resolvedPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<GrindingDepthBaseline>(json, SerializerOptions);
    }

    /// <summary>
    /// 基于已保存的检测基线重新采集代表廓形并检测已打磨深度。
    /// </summary>
    /// <param name="baseline">待比较的基线数据。</param>
    /// <returns>每个角度对应的已打磨深度结果集合。</returns>
    public static IReadOnlyList<DetectedGrindDepthResult> DetectGrindingDepths(GrindingDepthBaseline baseline)
    {
        if (baseline == null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        if (baseline.Angles == null || baseline.Angles.Count == 0)
        {
            throw new InvalidOperationException("基线角度列表为空。");
        }

        IReadOnlyList<RailProfilePoint> representativePoints = CaptureRepresentativeSectionPoints();
        IReadOnlyList<AngleBValue> currentBValues = CalculateBValues(baseline.Angles, representativePoints);

        var baselineMap = baseline.BaselineBValues.ToDictionary(item => item.Angle, item => item.B);
        var results = new List<DetectedGrindDepthResult>(currentBValues.Count);

        for (int index = 0; index < currentBValues.Count; index++)
        {
            AngleBValue currentValue = currentBValues[index];
            if (!baselineMap.TryGetValue(currentValue.Angle, out double baselineB))
            {
                throw new InvalidOperationException($"检测基线中缺少角度 {currentValue.Angle} 的 b 值。");
            }

            double detectedDepth = Math.Abs(currentValue.B - baselineB);
            results.Add(new DetectedGrindDepthResult(currentValue.Angle, baselineB, currentValue.B, detectedDepth));
        }

        return results;
    }

    /// <summary>
    /// 基于给定代表截面点集计算指定角度对应的 b 值。
    /// </summary>
    /// <param name="angle">打磨角度，单位为度。</param>
    /// <param name="representativeSectionPoints">代表截面点集。</param>
    /// <returns>对应角度下的支撑直线截距。</returns>
    public static double CalculateBByAngle(int angle, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        double k = CalculateSlopeFromAngle(angle);
        return GetB(k, representativeSectionPoints);
    }

    /// <summary>
    /// 基于已获取的代表截面点集计算指定角度的打磨深度。
    /// </summary> 
    /// <param name="angle">打磨角度，单位为度。</param>
    /// <param name="representativeSectionPoints">代表截面点集。</param>
    /// <returns>对应的打磨深度。</returns>
    private static double GetGrindDepth(int angle, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        double k = CalculateSlopeFromAngle(angle);
        return Math.Abs(GetB(k, representativeSectionPoints) - SolveB(k));
    }

    /// <summary>
    /// 将打磨角度转换为直线斜率。
    /// </summary>
    /// <param name="angle">打磨角度，单位为度。</param>
    /// <returns>对应的斜率值。</returns>
    private static double CalculateSlopeFromAngle(int angle)
    {
        // Math.Tan 接收弧度，因此需要先将角度转换为弧度。
        double radians = (StraightAngleDegrees + angle) * DegreesToRadiansFactor;
        return Math.Tan(radians);
    }
    

    /// <summary>
    /// 从所有可用点云设备采集并合并代表截面点集。
    /// </summary>
    /// <returns>合并后的代表截面点集合。</returns>
    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints()
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
        
        // if (pointCloudDeviceInfos.Count == 1)
        // {
        //     var result = new List<RailProfilePoint>();
        //     for (int i = 0; i < mergedPoints.Count; i++)
        //     {
        //         result.Add(new RailProfilePoint(mergedPoints[i].X, mergedPoints[i].Y));
        //         result.Add(new RailProfilePoint(-1 * mergedPoints[i].X,mergedPoints[i].Y));
        //     }
        //     return result;
        // }

        if (mergedPoints.Count == 0)
        {
            throw new InvalidOperationException("未能从任何点云设备提取到有效的中位截面点。");
        }

        return mergedPoints;
    }

    /// <summary>
    /// 基于给定代表截面点集计算支撑直线截距。
    /// </summary>
    /// <param name="k">目标直线斜率。</param>
    /// <param name="representativeSectionPoints">代表截面点集。</param>
    /// <returns>支撑直线截距值。</returns>
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
    /// <param name="k">目标直线斜率。</param>
    /// <returns>与标准轨面第一次接触时的截距值。</returns>
    public static double SolveB(double k)
    {
        return SolveBAndTouchPoint(k).b;
    }

    /// <summary>
    /// 返回 b 以及第一次接触点 x（方便你调试/验证）
    /// </summary>
    /// <param name="k">目标直线斜率。</param>
    /// <returns>包含截距值和接触点横向坐标的元组。</returns>
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

    /// <summary>
    /// 解析检测基线文件路径。
    /// </summary>
    /// <param name="baselineFilePath">外部指定的文件路径。</param>
    /// <returns>最终使用的完整文件路径。</returns>
    private static string ResolveBaselineFilePath(string? baselineFilePath)
    {
        return string.IsNullOrWhiteSpace(baselineFilePath)
            ? Path.Combine(Environment.CurrentDirectory, DefaultLogDirectoryName, DefaultBaselineFileName)
            : baselineFilePath;
    }
}
