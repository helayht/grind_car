using System;
using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

/// <summary>
/// 同一份点云的一次性代表截面与最大掉块联合分析结果。
/// </summary>
internal sealed record PointCloudProfileAnalysisResult(
    MedianSectionExtractionResult ExtractionResult,
    MaximumDropProfileResult? MaximumDropProfile);

internal interface IPointCloudProfileAnalysisService
{
    PointCloudProfileAnalysisResult AnalyzePoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side,
        int sampleIndex = 0);

    PointCloudProfileAnalysisResult AnalyzeCsv(
        string csvPath,
        PointCloudDeviceSide side,
        int sampleIndex = 0);
}

/// <summary>
/// 只执行一次公共预处理，再顺序运行代表截面与最大掉块算法。
/// </summary>
internal sealed class PointCloudProfileAnalysisService : IPointCloudProfileAnalysisService
{
    private readonly IPointCloudProfilePreprocessor _preprocessor;
    private readonly PointCloudRepresentativeProfileService _representativeProfileService;
    private readonly MaximumDropProfileService _maximumDropProfileService;
    private readonly Func<string, List<PointCloudPoint3D>> _csvReader;

    public PointCloudProfileAnalysisService()
    {
        var registrationSettingsStore = new ProfileRegistrationSettingsStore();
        var registrationTransformService = new ProfileRegistrationTransformService();
        var preprocessor = new PointCloudProfilePreprocessor(
            registrationSettingsStore,
            registrationTransformService);

        _preprocessor = preprocessor;
        _representativeProfileService = new PointCloudRepresentativeProfileService(
            preprocessor,
            registrationTransformService);
        _maximumDropProfileService = new MaximumDropProfileService(
            preprocessor,
            registrationTransformService);
        _csvReader = PointCloudCsvReader.ReadPointsFromCsv;
    }

    internal PointCloudProfileAnalysisService(
        IPointCloudProfilePreprocessor preprocessor,
        PointCloudRepresentativeProfileService representativeProfileService,
        MaximumDropProfileService maximumDropProfileService,
        Func<string, List<PointCloudPoint3D>>? csvReader = null)
    {
        _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        _representativeProfileService = representativeProfileService ??
            throw new ArgumentNullException(nameof(representativeProfileService));
        _maximumDropProfileService = maximumDropProfileService ??
            throw new ArgumentNullException(nameof(maximumDropProfileService));
        _csvReader = csvReader ?? PointCloudCsvReader.ReadPointsFromCsv;
    }

    public PointCloudProfileAnalysisResult AnalyzePoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side,
        int sampleIndex = 0)
    {
        PreparedPointCloudProfileData preparedData = _preprocessor.Prepare(points, side);
        MedianSectionExtractionResult extractionResult =
            _representativeProfileService.ExtractMedianSectionProfile(preparedData);
        MaximumDropProfileResult? maximumDropProfile =
            _maximumDropProfileService.AnalyzePrepared(preparedData, sampleIndex);
        return new PointCloudProfileAnalysisResult(extractionResult, maximumDropProfile);
    }

    public PointCloudProfileAnalysisResult AnalyzeCsv(
        string csvPath,
        PointCloudDeviceSide side,
        int sampleIndex = 0)
    {
        List<PointCloudPoint3D> points = _csvReader(csvPath);
        return AnalyzePoints(points, side, sampleIndex);
    }
}
