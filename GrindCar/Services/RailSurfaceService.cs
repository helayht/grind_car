using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services;

/// <summary>
/// 提供标准轨面函数、代表截面采集与打磨深度计算能力。
/// </summary>
public static class RailSurfaceService
{
    private const double StraightAngleDegrees = 180.0;
    private const double DegreesToRadiansFactor = Math.PI / 180.0;

    /// <summary>
    /// 根据给定横向坐标计算标准轨面函数值。
    /// </summary>
    public static double RailSurfaceFun(double x)
    {
        return StandardRailProfileSolver.RailSurfaceFun(x);
    }

    /// <summary>
    /// 批量计算打磨深度，并返回过程中使用的代表截面点集。
    /// </summary>
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
        int[] angleArray = new int[angles.Count];
        for (int index = 0; index < angles.Count; index++)
        {
            angleArray[index] = angles[index];
        }

        return new GrindingDepthBaseline(DateTime.Now, angleArray, baselineBValues);
    }

    /// <summary>
    /// 保存最新检测基线数据。
    /// </summary>
    public static void SaveGrindingDepthBaseline(GrindingDepthBaseline baseline, string? baselineFilePath = null)
    {
        GrindingDepthBaselineStore.Save(baseline, baselineFilePath);
    }

    /// <summary>
    /// 加载最新检测基线数据。
    /// </summary>
    public static GrindingDepthBaseline? LoadLatestGrindingDepthBaseline(string? baselineFilePath = null)
    {
        return GrindingDepthBaselineStore.LoadLatest(baselineFilePath);
    }

    /// <summary>
    /// 基于已保存的检测基线重新采集代表廓形并检测已打磨深度。
    /// </summary>
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

        var baselineMap = new Dictionary<int, double>(baseline.BaselineBValues.Count);
        for (int index = 0; index < baseline.BaselineBValues.Count; index++)
        {
            AngleBValue item = baseline.BaselineBValues[index];
            baselineMap[item.Angle] = item.B;
        }

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
    public static double CalculateBByAngle(int angle, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        double k = CalculateSlopeFromAngle(angle);
        return StandardRailProfileSolver.GetRepresentativeB(k, representativeSectionPoints);
    }

    /// <summary>
    /// 从所有可用点云设备采集并合并代表截面点集。
    /// </summary>
    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints()
    {
        return RepresentativeSectionCaptureService.CaptureRepresentativeSectionPoints();
    }

    /// <summary>
    /// 给定斜率 k，返回第一次接触时的最小 b，使得 y=kx+b 在轨面上方且刚好相切/接触。
    /// </summary>
    public static double SolveB(double k)
    {
        return StandardRailProfileSolver.SolveB(k);
    }

    /// <summary>
    /// 返回 b 以及第一次接触点 x（便于调试验证）。
    /// </summary>
    public static (double b, double xTouch) SolveBAndTouchPoint(double k)
    {
        return StandardRailProfileSolver.SolveBAndTouchPoint(k);
    }

    private static double GetGrindDepth(int angle, IReadOnlyList<RailProfilePoint> representativeSectionPoints)
    {
        double k = CalculateSlopeFromAngle(angle);
        double representativeB = StandardRailProfileSolver.GetRepresentativeB(k, representativeSectionPoints);
        double standardB = StandardRailProfileSolver.SolveB(k);
        return Math.Abs(representativeB - standardB);
    }

    private static double CalculateSlopeFromAngle(int angle)
    {
        double radians = (StraightAngleDegrees + angle) * DegreesToRadiansFactor;
        return Math.Tan(radians);
    }
}
