namespace GrindCar.Models.Rail;

/// <summary>
/// 单帧点云采集并提取代表截面的结果。
/// </summary>
public sealed class PointCloudMedianSectionCaptureResult
{
    /// <summary>
    /// 初始化单帧点云采集与截面提取结果。
    /// </summary>
    /// <param name="csvPath">导出的点云 CSV 路径；在线模式下为空字符串。</param>
    /// <param name="extractionResult">代表截面提取结果。</param>
    public PointCloudMedianSectionCaptureResult(string csvPath, MedianSectionExtractionResult extractionResult)
    {
        CsvPath = csvPath;
        ExtractionResult = extractionResult;
    }

    public string CsvPath { get; }

    public MedianSectionExtractionResult ExtractionResult { get; }
}
