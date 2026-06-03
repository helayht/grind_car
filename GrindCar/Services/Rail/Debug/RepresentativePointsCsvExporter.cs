using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail.Debug;

/// <summary>
/// 导出代表点 CSV。
/// </summary>
public static class RepresentativePointsCsvExporter
{
    public static void Export(string outputPath, IReadOnlyList<RailProfilePoint> points)
    {
        RailProfilePointCsvExporter.Export(
            outputPath,
            points,
            "X,Y",
            "导出路径不能为空。",
            "当前没有可导出的代表点，请先执行“计算需要打磨深度”。",
            "导出路径无效。");
    }
}
