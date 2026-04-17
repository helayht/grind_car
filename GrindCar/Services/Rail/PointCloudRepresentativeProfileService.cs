using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

/// <summary>
/// 从点云 CSV 中提取代表廓形二维点集。
/// 输出的 RailProfilePoint 语义为：
/// X -> 轨面横向 X
/// Y -> 高度 Z
/// </summary>
public sealed class PointCloudRepresentativeProfileService : IPointCloudRepresentativeProfileService
{
    private const double Tolerance = 1e-7;
    private const double RepresentativeRotationDegrees = 28.5;
    private const double DegreesToRadiansFactor = Math.PI / 180.0;

    /// <summary>
    /// 从点云 CSV 文件中提取中位 Y 截面的二维 X/Z 点集。
    /// </summary>
    /// <param name="csvPath">点云 CSV 文件路径。</param>
    /// <returns>包含中位 Y 值和对应二维点集的提取结果。</returns>
    public MedianSectionExtractionResult ExtractMedianSectionProfileFromCsv(string csvPath)
    {
        try
        {
            List<PointCloudPoint3D> points = PointCloudCsvReader.ReadPointsFromCsv(csvPath);
            if (points.Count == 0)
            {
                throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效坐标点。");
            }

            double medianY = ResolveMedianY(points);
            List<RailProfilePoint> sectionPoints = CreateSectionPoints(points, medianY);
            if (sectionPoints.Count == 0)
            {
                throw new RepresentativeProfileExtractionException("未找到中位 Y 截面的有效 X/Z 点。");
            }

            List<RailProfilePoint> filteredSectionPoints =
                RepresentativeProfilePointProcessor.FilterOutlierRepresentativePoints(sectionPoints);
            List<RailProfilePoint> rotatedSectionPoints =
                RepresentativeProfilePointProcessor.RotateRepresentativePoints(
                    filteredSectionPoints,
                    RepresentativeRotationDegrees * DegreesToRadiansFactor);
            List<RailProfilePoint> symmetricSectionPoints =
                RepresentativeProfilePointProcessor.AppendSymmetricPointsByMinX(rotatedSectionPoints);
            List<RailProfilePoint> translatedSectionPoints =
                RepresentativeProfilePointProcessor.TranslatePointsToBottomCenterAsOrigin(symmetricSectionPoints);

            return new MedianSectionExtractionResult(medianY, translatedSectionPoints);
        }
        catch (RepresentativeProfileExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RepresentativeProfileExtractionException("从 CSV 提取中位 Y 截面时发生未处理异常。", ex);
        }
    }

    private static double ResolveMedianY(IReadOnlyList<PointCloudPoint3D> points)
    {
        var uniqueYSet = new HashSet<double>();
        for (int index = 0; index < points.Count; index++)
        {
            uniqueYSet.Add(points[index].Y);
        }

        double[] uniqueYValues = new double[uniqueYSet.Count];
        uniqueYSet.CopyTo(uniqueYValues);
        if (uniqueYValues.Length == 0)
        {
            throw new RepresentativeProfileExtractionException("点云 CSV 中未解析到有效的 Y 坐标。");
        }

        int medianIndex = (uniqueYValues.Length - 1) / 2;
        return QuickSelect.SelectKthSmallest(uniqueYValues, medianIndex);
    }

    private static List<RailProfilePoint> CreateSectionPoints(IReadOnlyList<PointCloudPoint3D> points, double medianY)
    {
        var sectionPoints = new List<RailProfilePoint>();
        for (int index = 0; index < points.Count; index++)
        {
            PointCloudPoint3D point = points[index];
            if (Math.Abs(point.Y - medianY) < Tolerance)
            {
                sectionPoints.Add(new RailProfilePoint(point.X, point.Z));
            }
        }

        return sectionPoints;
    }
}