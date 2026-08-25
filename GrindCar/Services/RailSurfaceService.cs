using System;
using System.Collections.Generic;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services;

/// <summary>
/// 提供标准轨面函数、代表截面采集与打磨深度计算能力。
/// </summary>
public static class RailSurfaceService
{
    private const double DegreesToRadiansFactor = Math.PI / 180.0;

    /// <summary>
    /// 根据给定横向坐标计算标准轨面函数值。
    /// </summary>
    public static double RailSurfaceFun(double x)
    {
        return StandardRailProfileSolver.RailSurfaceFun(x);
    }

    /// <summary>
    /// 当前激活的轨面型号。
    /// </summary>
    public static RailProfileType CurrentProfileType => StandardRailProfileSolver.CurrentProfileType;

    /// <summary>
    /// 切换轨面型号，内部会清空切线求解缓存。
    /// </summary>
    public static void SwitchProfile(RailProfileType type)
    {
        StandardRailProfileSolver.SwitchProfile(type);
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

        PointCloudCaptureSettings captureSettings = LoadPointCloudCaptureSettings();
        IReadOnlyList<RailProfilePoint> representativeSectionPoints = CaptureRepresentativeSectionPoints(captureSettings);
        return CalculateGrindDepths(angles, representativeSectionPoints);
    }

    /// <summary>
    /// 基于已采集的代表截面点集批量计算打磨深度。
    /// </summary>
    public static GrindDepthCalculationResult CalculateGrindDepths(
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

        if (angles.Count == 0)
        {
            return new GrindDepthCalculationResult(Array.Empty<GrindDepthResult>(), representativeSectionPoints);
        }

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
    /// 基于单侧最大掉块廓形计算各角度的附加打磨深度。
    /// 仅将标准轨面法向支撑值高于测量廓形的部分计为向下缺失深度。
    /// </summary>
    public static IReadOnlyList<GrindDepthResult> CalculateDefectGrindDepths(
        IReadOnlyList<int> angles,
        IReadOnlyList<RailProfilePoint> adjustedDefectProfilePoints)
    {
        if (angles == null)
        {
            throw new ArgumentNullException(nameof(angles));
        }

        if (adjustedDefectProfilePoints == null)
        {
            throw new ArgumentNullException(nameof(adjustedDefectProfilePoints));
        }

        if (adjustedDefectProfilePoints.Count == 0)
        {
            throw new InvalidOperationException("最大掉块廓形点集不能为空。");
        }

        var results = new List<GrindDepthResult>(angles.Count);
        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            double angleRadians = CalculateAngleRadians(angle);
            double profileOffset = StandardRailProfileSolver.GetRepresentativeNormalOffset(
                angleRadians,
                adjustedDefectProfilePoints);
            double standardOffset = StandardRailProfileSolver.SolveNormalOffset(angleRadians);
            double defectDepth = Math.Max(0.0, standardOffset - profileOffset);
            results.Add(new GrindDepthResult(angle, defectDepth));
        }

        return results;
    }

    /// <summary>
    /// 从所有可用点云设备采集并合并代表截面点集。
    /// </summary>
    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints()
    {
        PointCloudCaptureSettings captureSettings = LoadPointCloudCaptureSettings();
        return CaptureRepresentativeSectionPoints(captureSettings);
    }

    /// <summary>
    /// 从所有可用点云设备按给定参数采集并合并代表截面点集。
    /// </summary>
    public static IReadOnlyList<RailProfilePoint> CaptureRepresentativeSectionPoints(PointCloudCaptureSettings captureSettings)
    {
        return RepresentativeSectionCaptureService.CaptureRepresentativeSectionPoints(captureSettings);
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
        double angleRadians = CalculateAngleRadians(angle);
        double representativeOffset = StandardRailProfileSolver.GetRepresentativeNormalOffset(
            angleRadians,
            representativeSectionPoints);
        double standardOffset = StandardRailProfileSolver.SolveNormalOffset(angleRadians);
        return Math.Abs(representativeOffset - standardOffset);
    }

    internal static bool IsDefectAngleApplicable(PointCloudDeviceSide side, int angle)
    {
        return side == PointCloudDeviceSide.Left ? angle >= 0 : angle <= 0;
    }

    private static double CalculateAngleRadians(int angle)
    {
        return angle * DegreesToRadiansFactor;
    }

    private static PointCloudCaptureSettings LoadPointCloudCaptureSettings()
    {
        return new PointCloudCaptureSettingsStore().LoadRequired();
    }
}
