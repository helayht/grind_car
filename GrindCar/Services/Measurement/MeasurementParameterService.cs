using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.PointCloud;
using GrindCar.Models.Rail;
using GrindCar.Services.PointCloud;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 通过 Modbus 写入测量参数。
/// </summary>
public class MeasurementParameterService : IMeasurementParameterService
{
    private const byte DefaultUnitId = 1;
    private const int DefaultPollIntervalMs = 300;
    private const double GrindingTimesDepthStep = 0.05;
    private readonly Func<string, int, IPlcClient> _plcClientFactory;
    private readonly Func<IReadOnlyList<ConfiguredPointCloudDevice>> _configuredDeviceProvider;
    private readonly Func<PointCloudCaptureSettings> _captureSettingsProvider;
    private readonly Func<ConfiguredPointCloudDevice, PointCloudCaptureSettings, MeasurementPointCloudArchiveContext?, PointCloudMedianSectionCaptureResult> _deviceAnalysisCapture;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult> _grindDepthCalculator;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, IReadOnlyList<GrindDepthResult>> _defectGrindDepthCalculator;

    public MeasurementParameterService()
        : this(CreateDefaultPlcClient)
    {
    }

    internal MeasurementParameterService(
        Func<string, int, IPlcClient> plcClientFactory,
        Func<IReadOnlyList<ConfiguredPointCloudDevice>>? configuredDeviceProvider = null,
        Func<PointCloudCaptureSettings>? captureSettingsProvider = null,
        Func<ConfiguredPointCloudDevice, PointCloudCaptureSettings, MeasurementPointCloudArchiveContext?, IReadOnlyList<RailProfilePoint>>? representativePointCapture = null,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, GrindDepthCalculationResult>? grindDepthCalculator = null,
        Func<ConfiguredPointCloudDevice, PointCloudCaptureSettings, MeasurementPointCloudArchiveContext?, PointCloudMedianSectionCaptureResult>? deviceAnalysisCapture = null,
        Func<IReadOnlyList<int>, IReadOnlyList<RailProfilePoint>, IReadOnlyList<GrindDepthResult>>? defectGrindDepthCalculator = null)
    {
        _plcClientFactory = plcClientFactory ?? throw new ArgumentNullException(nameof(plcClientFactory));
        _configuredDeviceProvider = configuredDeviceProvider ?? RepresentativeSectionCaptureService.GetAvailableConfiguredDevicesInOrder;
        _captureSettingsProvider = captureSettingsProvider ?? (() => new PointCloudCaptureSettingsStore().LoadRequired());
        if (deviceAnalysisCapture != null)
        {
            _deviceAnalysisCapture = deviceAnalysisCapture;
        }
        else if (representativePointCapture != null)
        {
            _deviceAnalysisCapture = (device, settings, archiveContext) =>
            {
                IReadOnlyList<RailProfilePoint> points =
                    representativePointCapture(device, settings, archiveContext);
                return new PointCloudMedianSectionCaptureResult(
                    string.Empty,
                    new MedianSectionExtractionResult(0.0, points));
            };
        }
        else
        {
            _deviceAnalysisCapture = RepresentativeSectionCaptureService.CaptureDeviceAnalysis;
        }

        _grindDepthCalculator = grindDepthCalculator ?? RailSurfaceService.CalculateGrindDepths;
        _defectGrindDepthCalculator = defectGrindDepthCalculator ?? RailSurfaceService.CalculateDefectGrindDepths;
    }

    public static int CalculateGrindingTimes(double grindDepth)
    {
        if (double.IsNaN(grindDepth) || double.IsInfinity(grindDepth))
        {
            throw new InvalidOperationException("打磨深度必须是有效数字。");
        }

        if (grindDepth < 0)
        {
            throw new InvalidOperationException("打磨深度不能小于 0。");
        }

        return (int)Math.Ceiling(grindDepth / GrindingTimesDepthStep);
    }

    public async Task WriteMeasurementRangeAsync(string ipAddress, int port, double startPosition, double endPosition)
    {
        if (startPosition > endPosition)
        {
            throw new InvalidOperationException("测量起点位置不能大于测量终点位置。");
        }

        int startRawValue = MeasurementInputParser.ToScaledInt32(
            startPosition,
            MotorParameterDefinitions.MeasurementStartPositionScale,
            MotorParameterDefinitions.MeasurementStartPositionName);
        int endRawValue = MeasurementInputParser.ToScaledInt32(
            endPosition,
            MotorParameterDefinitions.MeasurementEndPositionScale,
            MotorParameterDefinitions.MeasurementEndPositionName);

        using IPlcClient plcClient = _plcClientFactory(ipAddress, port);
        await plcClient.ConnectAsync().ConfigureAwait(false);
        plcClient.WriteInt32(MotorParameterDefinitions.MeasurementStartPositionAddress, startRawValue);
        plcClient.WriteInt32(MotorParameterDefinitions.MeasurementEndPositionAddress, endRawValue);
        plcClient.Disconnect();
    }

    public async Task WriteGrindingRangeAsync(string ipAddress, int port, double startPosition, double endPosition)
    {
        if (startPosition > endPosition)
        {
            throw new InvalidOperationException("打磨起点位置不能大于打磨终点位置。");
        }

        int startRawValue = MeasurementInputParser.ToScaledInt32(
            startPosition,
            MotorParameterDefinitions.GrindingStartPositionScale,
            MotorParameterDefinitions.GrindingStartPositionName);
        int endRawValue = MeasurementInputParser.ToScaledInt32(
            endPosition,
            MotorParameterDefinitions.GrindingEndPositionScale,
            MotorParameterDefinitions.GrindingEndPositionName);

        using IPlcClient plcClient = _plcClientFactory(ipAddress, port);
        await plcClient.ConnectAsync().ConfigureAwait(false);
        plcClient.WriteInt32(MotorParameterDefinitions.GrindingStartPositionAddress, startRawValue);
        plcClient.WriteInt32(MotorParameterDefinitions.GrindingEndPositionAddress, endRawValue);
        plcClient.Disconnect();
    }

    public async Task StartMeasurementMotionAsync(string ipAddress, int port)
    {
        using IPlcClient plcClient = _plcClientFactory(ipAddress, port);
        await plcClient.ConnectAsync().ConfigureAwait(false);
        await plcClient.WriteSingleCoilAsync(MotorParameterDefinitions.MeasurementMotionStartAddress, true)
            .ConfigureAwait(false);
        plcClient.Disconnect();
    }

    public async Task StartGrindingMotionAsync(string ipAddress, int port)
    {
        using IPlcClient plcClient = _plcClientFactory(ipAddress, port);
        await plcClient.ConnectAsync().ConfigureAwait(false);
        await plcClient.WriteSingleCoilAsync(MotorParameterDefinitions.GrindingMotionStartAddress, true)
            .ConfigureAwait(false);
        plcClient.Disconnect();
    }

    public async Task WriteGrindingTimesAsync(
        string ipAddress,
        int port,
        IReadOnlyList<MeasurementGrindingTimesResult> results)
    {
        if (results.Count == 0)
        {
            throw new InvalidOperationException("没有可写入的打磨次数。");
        }

        using IPlcClient plcClient = _plcClientFactory(ipAddress, port);
        await plcClient.ConnectAsync().ConfigureAwait(false);

        for (int index = 0; index < results.Count; index++)
        {
            MeasurementGrindingTimesResult result = results[index];
            if (!MotorParameterDefinitions.MeasurementGrindingTimesAddresses.TryGetValue(result.Angle, out ushort address))
            {
                throw new InvalidOperationException($"角度 {result.Angle} 未配置打磨次数写入地址。");
            }

            plcClient.WriteInt32(address, result.GrindingTimes);
        }

        plcClient.Disconnect();
    }

    public async Task<MeasurementGrindingWorkflowResult> RunMeasurementWorkflowAsync(
        string ipAddress,
        int port,
        IProgress<string>? progress = null,
        Action? measurementEnded = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<int> angles = MotorParameterDefinitions.MeasurementGrindingAngles;
        PointCloudCaptureSettings captureSettings = _captureSettingsProvider();
        IReadOnlyList<ConfiguredPointCloudDevice> configuredDevices = _configuredDeviceProvider();
        if (configuredDevices.Count != 2)
        {
            throw new InvalidOperationException(
                $"当前测量流程要求配置 2 台廓形仪，实际配置 {configuredDevices.Count.ToString(CultureInfo.InvariantCulture)} 台。");
        }

        var depthAccumulatorMap = CreateDepthAccumulatorMap(angles);
        var pendingRepresentativePoints = new List<RailProfilePoint>();
        MaximumDropProfileResult? pendingLeftDropProfile = null;
        MaximumDropProfileResult? pendingRightDropProfile = null;
        MaximumDropProfileResult? maximumLeftDropProfile = null;
        MaximumDropProfileResult? maximumRightDropProfile = null;
        int nextDeviceIndex = 0;

        using IPlcClient plcClient = _plcClientFactory(ipAddress, port);
        await plcClient.ConnectAsync().ConfigureAwait(false);

        Report(progress, "已连接 PLC，准备启动测量运行。");
        await plcClient.WriteSingleCoilAsync(MotorParameterDefinitions.MeasurementMotionStartAddress, true)
            .ConfigureAwait(false);
        Report(progress, "已写入测量运行启动信号，开始等待触发。");

        int sampleCount = 0;
        bool lastCaptureTrigger = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool positionCompleted = plcClient.ReadSingleCoil(MotorParameterDefinitions.MeasurementPositionCompletedAddress);
            if (positionCompleted)
            {
                measurementEnded?.Invoke();
                Report(progress, "检测到测量定位完成信号，开始汇总打磨深度。");
                break;
            }

            bool captureTrigger = plcClient.ReadSingleCoil(MotorParameterDefinitions.MeasurementProfileCaptureStartAddress);
            if (!captureTrigger || lastCaptureTrigger)
            {
                lastCaptureTrigger = captureTrigger;
                await Task.Delay(DefaultPollIntervalMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            lastCaptureTrigger = captureTrigger;
            ConfiguredPointCloudDevice device = configuredDevices[nextDeviceIndex];
            Report(
                progress,
                $"检测到当前廓形测量启动上升沿，正在采集第 {nextDeviceIndex + 1} 台廓形仪 ({device.Side})。");

            var archiveContext = new MeasurementPointCloudArchiveContext(
                sampleCount + 1,
                nextDeviceIndex + 1,
                device.SerialNumber,
                device.Side,
                progress);
            PointCloudMedianSectionCaptureResult deviceAnalysis = _deviceAnalysisCapture(
                device,
                captureSettings,
                archiveContext);
            IReadOnlyList<RailProfilePoint> devicePoints = deviceAnalysis.ExtractionResult.ProfilePoints;
            if (devicePoints.Count > 0)
            {
                pendingRepresentativePoints.AddRange(devicePoints);
            }

            if (deviceAnalysis.MaximumDropProfile != null)
            {
                if (device.Side == PointCloudDeviceSide.Left)
                {
                    pendingLeftDropProfile = MaximumDropProfileService.SelectDeeper(
                        pendingLeftDropProfile,
                        deviceAnalysis.MaximumDropProfile);
                }
                else
                {
                    pendingRightDropProfile = MaximumDropProfileService.SelectDeeper(
                        pendingRightDropProfile,
                        deviceAnalysis.MaximumDropProfile);
                }
            }

            await plcClient.WriteSingleCoilAsync(MotorParameterDefinitions.MeasurementCurrentProfileCompletedAddress, true)
                .ConfigureAwait(false);
            Report(progress, $"第 {nextDeviceIndex + 1} 台廓形仪测量完成，已写入当前段廓形测量完成信号。");

            nextDeviceIndex++;
            if (nextDeviceIndex < configuredDevices.Count)
            {
                continue;
            }

            if (pendingRepresentativePoints.Count == 0)
            {
                throw new InvalidOperationException("本组测量未能从两台廓形仪提取到有效的代表截面点。");
            }

            Report(progress, $"两台廓形仪测量完成，正在计算第 {sampleCount + 1} 组打磨深度。");
            GrindDepthCalculationResult calculationResult =
                _grindDepthCalculator(angles, pendingRepresentativePoints);
            AccumulateSingleMeasurement(depthAccumulatorMap, calculationResult.Results);
            if (pendingLeftDropProfile != null)
            {
                maximumLeftDropProfile = MaximumDropProfileService.SelectDeeper(
                    maximumLeftDropProfile,
                    pendingLeftDropProfile);
            }

            if (pendingRightDropProfile != null)
            {
                maximumRightDropProfile = MaximumDropProfileService.SelectDeeper(
                    maximumRightDropProfile,
                    pendingRightDropProfile);
            }

            sampleCount++;
            pendingRepresentativePoints.Clear();
            pendingLeftDropProfile = null;
            pendingRightDropProfile = null;
            nextDeviceIndex = 0;
            Report(progress, $"第 {sampleCount} 组测量计算完成。");
        }

        if (nextDeviceIndex != 0)
        {
            Report(progress, "检测到未完成的半组测量数据，已忽略该半组。");
        }

        if (sampleCount == 0)
        {
            throw new InvalidOperationException("测量运行已结束，但未采集到任何单次测量结果。");
        }

        IReadOnlyList<MeasurementGrindingTimesResult> summaryResults = CalculateSummaryResults(
            depthAccumulatorMap,
            sampleCount,
            angles,
            maximumLeftDropProfile,
            maximumRightDropProfile);

        MaximumDropProfileResult? globalMaximumDropProfile = ResolveGlobalMaximumDropProfile(
            maximumLeftDropProfile,
            maximumRightDropProfile);

        Report(progress, $"打磨深度汇总完成，共得到 {summaryResults.Count.ToString(CultureInfo.InvariantCulture)} 个角度。");
        plcClient.Disconnect();
        return new MeasurementGrindingWorkflowResult(
            sampleCount,
            summaryResults,
            globalMaximumDropProfile);
    }

    private static Dictionary<int, DepthAccumulator> CreateDepthAccumulatorMap(IReadOnlyList<int> angles)
    {
        var accumulatorMap = new Dictionary<int, DepthAccumulator>(angles.Count);
        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            accumulatorMap[angle] = new DepthAccumulator();
        }

        return accumulatorMap;
    }

    private static void AccumulateSingleMeasurement(
        IDictionary<int, DepthAccumulator> depthAccumulatorMap,
        IReadOnlyList<GrindDepthResult> singleMeasurementResults)
    {
        for (int index = 0; index < singleMeasurementResults.Count; index++)
        {
            GrindDepthResult result = singleMeasurementResults[index];
            if (!depthAccumulatorMap.TryGetValue(result.Angle, out DepthAccumulator? accumulator))
            {
                throw new InvalidOperationException($"单次测量结果中存在未配置角度：{result.Angle}");
            }

            accumulator.Add(result.GrindDepth);
        }
    }

    private IReadOnlyList<MeasurementGrindingTimesResult> CalculateSummaryResults(
        IReadOnlyDictionary<int, DepthAccumulator> depthAccumulatorMap,
        int sampleCount,
        IReadOnlyList<int> angles,
        MaximumDropProfileResult? maximumLeftDropProfile,
        MaximumDropProfileResult? maximumRightDropProfile)
    {
        var results = new List<MeasurementGrindingTimesResult>(angles.Count);
        IReadOnlyDictionary<int, double> leftDefectDepthMap = CalculateDefectDepthMap(
            angles,
            maximumLeftDropProfile);
        IReadOnlyDictionary<int, double> rightDefectDepthMap = CalculateDefectDepthMap(
            angles,
            maximumRightDropProfile);

        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            if (!depthAccumulatorMap.TryGetValue(angle, out DepthAccumulator? accumulator))
            {
                throw new InvalidOperationException($"角度 {angle} 缺少累计信息。");
            }

            if (accumulator.Count == 0)
            {
                throw new InvalidOperationException($"角度 {angle} 未采集到有效测量值。");
            }

            double averageDepth = accumulator.SumDepth / accumulator.Count;
            double defectDepth = Math.Max(
                leftDefectDepthMap[angle],
                rightDefectDepthMap[angle]);
            double finalDepth = Math.Max(averageDepth, defectDepth);
            int grindingTimes = CalculateGrindingTimes(finalDepth);
            results.Add(new MeasurementGrindingTimesResult(
                angle,
                averageDepth,
                defectDepth,
                finalDepth,
                grindingTimes));
        }

        if (sampleCount <= 0)
        {
            throw new InvalidOperationException("累计测量次数无效。");
        }

        return results;
    }

    private IReadOnlyDictionary<int, double> CalculateDefectDepthMap(
        IReadOnlyList<int> angles,
        MaximumDropProfileResult? maximumDropProfile)
    {
        var depthMap = new Dictionary<int, double>(angles.Count);
        var applicableAngles = new List<int>();
        for (int index = 0; index < angles.Count; index++)
        {
            int angle = angles[index];
            depthMap[angle] = 0.0;
            if (maximumDropProfile != null &&
                RailSurfaceService.IsDefectAngleApplicable(maximumDropProfile.Side, angle))
            {
                applicableAngles.Add(angle);
            }
        }

        if (maximumDropProfile == null || applicableAngles.Count == 0)
        {
            return depthMap;
        }

        IReadOnlyList<GrindDepthResult> results = _defectGrindDepthCalculator(
            applicableAngles,
            maximumDropProfile.ProfilePoints);
        var calculatedAngles = new HashSet<int>();
        for (int index = 0; index < results.Count; index++)
        {
            depthMap[results[index].Angle] = results[index].GrindDepth;
            calculatedAngles.Add(results[index].Angle);
        }

        for (int index = 0; index < applicableAngles.Count; index++)
        {
            if (!calculatedAngles.Contains(applicableAngles[index]))
            {
                throw new InvalidOperationException($"掉块打磨深度结果缺少角度 {applicableAngles[index]}。");
            }
        }

        return depthMap;
    }

    private static MaximumDropProfileResult? ResolveGlobalMaximumDropProfile(
        MaximumDropProfileResult? left,
        MaximumDropProfileResult? right)
    {
        if (left == null)
        {
            return right;
        }

        if (right == null)
        {
            return left;
        }

        return left.MaximumDropDepth >= right.MaximumDropDepth ? left : right;
    }

    private static void Report(IProgress<string>? progress, string message)
    {
        progress?.Report(message);
    }

    private static IPlcClient CreateDefaultPlcClient(string ipAddress, int port)
    {
        return new PlcModbusCommunicator(ipAddress, port, DefaultUnitId);
    }

    private sealed class DepthAccumulator
    {
        public int Count { get; private set; }

        public double SumDepth { get; private set; }

        public void Add(double depth)
        {
            SumDepth += depth;
            Count++;
        }
    }
}
