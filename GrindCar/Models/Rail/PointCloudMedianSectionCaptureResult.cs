namespace GrindCar.Models.Rail;

/// <summary>
/// 单帧点云采集并提取中位 X 截面的结果。
/// </summary>
public sealed class PointCloudMedianSectionCaptureResult
{
    public PointCloudMedianSectionCaptureResult(string csvPath, MedianSectionExtractionResult extractionResult)
    {
        CsvPath = csvPath;
        ExtractionResult = extractionResult;
    }

    public string CsvPath { get; }

    public MedianSectionExtractionResult ExtractionResult { get; }
}
