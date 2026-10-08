using System.Collections.Generic;
using GrindCar.Models.Grinding;

namespace GrindCar.Definitions;

public static class MotorParameterDefinitions
{
    public const ushort MAddressOffset = 8192;

    public const string UnitMeter = "m";
    public const string UnitMillimeter = "mm";
    public const string UnitDegree = "°";
    public const string UnitMeterPerMinute = "m/min";
    public const string UnitMillimeterPerMinute = "mm/min";
    public const string UnitDegreePerMinute = "°/min";
    public const string UnitRadianPerMinute = "rad/min";

    // 参数名称
    public const string CarCurrentPositionName = "小车当前位置";
    public const string WheelLongitudinalCurrentPositionName = "砂轮纵向当前位置";
    public const string WheelLateralCurrentPositionName = "砂轮横向当前位置";
    public const string WheelAngleCurrentPositionName = "砂轮角度当前位置";
    public const string ProfilerCurrentPositionName = "廓形仪当前位置";

    public const string CarJogSpeedName = "小车点动速度";
    public const string CarPositionTargetAddressName = "小车定位运行地址";
    public const string CarPositionSpeedName = "小车定位运行速度";
    public const string CarPositionStartName = "小车定位运行启动";
    public const string CarJogForwardName = "小车点动前进";
    public const string CarJogBackwardName = "小车点动后退";
    public const string CarHomeName = "小车定位回零";
    public const string CarFaultResetName = "小车故障复位";

    public const string WheelLongitudinalJogSpeedName = "砂轮纵向点动速度";
    public const string WheelLongitudinalPositionTargetAddressName = "砂轮纵向定位运行地址";
    public const string WheelLongitudinalPositionSpeedName = "砂轮纵向定位运行速度";
    public const string WheelLongitudinalPositionStartName = "砂轮纵向定位运行启动";
    public const string WheelLongitudinalJogDownName = "砂轮纵向点动下行";
    public const string WheelLongitudinalJogUpName = "砂轮纵向点动上行";
    public const string WheelLongitudinalHomeName = "砂轮纵向定位回零";
    public const string WheelLongitudinalFaultResetName = "砂轮纵向故障复位";

    public const string WheelLateralJogSpeedName = "砂轮横向点动速度";
    public const string WheelLateralPositionTargetAddressName = "砂轮横向定位运行地址";
    public const string WheelLateralPositionSpeedName = "砂轮横向定位运行速度";
    public const string WheelLateralPositionStartName = "砂轮横向定位运行启动";
    public const string WheelLateralJogLeftName = "砂轮横向点动左行";
    public const string WheelLateralJogRightName = "砂轮横向点动右行";
    public const string WheelLateralHomeName = "砂轮横向定位回零";
    public const string WheelLateralFaultResetName = "砂轮横向故障复位";

    public const string WheelAngleJogSpeedName = "砂轮角度点动速度";
    public const string WheelAnglePositionTargetAddressName = "砂轮角度定位运行地址";
    public const string WheelAnglePositionSpeedName = "砂轮角度定位运行速度";
    public const string WheelAnglePositionStartName = "砂轮角度定位运行启动";
    public const string WheelAngleJogReverseName = "砂轮角度点动逆行";
    public const string WheelAngleJogForwardName = "砂轮角度点动顺行";
    public const string WheelAngleHomeName = "砂轮角度定位回零";
    public const string WheelAngleFaultResetName = "砂轮角度故障复位";

    public const string ProfilerJogSpeedName = "廓形仪点动速度";
    public const string ProfilerPositionTargetAddressName = "廓形仪定位运行地址";
    public const string ProfilerPositionSpeedName = "廓形仪定位运行速度";
    public const string ProfilerPositionStartName = "廓形仪定位运行启动";
    public const string ProfilerJogDownName = "廓形仪点动下行";
    public const string ProfilerJogUpName = "廓形仪点动上行";
    public const string ProfilerHomeName = "廓形仪定位回零";
    public const string ProfilerFaultResetName = "廓形仪故障复位";

    public const string WheelRotationSpeedName = "砂轮旋转速度";
    public const string WheelRunCommandName = "砂轮运行指令";
    public const string WheelRunExecutionName = "砂轮运行执行";
    public const string MeasurementStartPositionName = "测量起点位置";
    public const string MeasurementEndPositionName = "测量终点位置";
    public const string GrindingStartPositionName = "打磨起点位置";
    public const string GrindingEndPositionName = "打磨终点位置";

    // 只读参数 Modbus 地址
    public const ushort CarCurrentPositionAddress = 100;
    public const ushort WheelLongitudinalCurrentPositionAddress = 300;
    public const ushort WheelLateralCurrentPositionAddress = 400;
    public const ushort WheelAngleCurrentPositionAddress = 500;
    public const ushort ProfilerCurrentPositionAddress = 600;

    // 只读参数比例
    public const double CarCurrentPositionScale = 100000.0;
    public const double WheelLongitudinalCurrentPositionScale = 10000.0;
    public const double WheelLateralCurrentPositionScale = 10000.0;
    public const double WheelAngleCurrentPositionScale = 10000.0;
    public const double ProfilerCurrentPositionScale = 10000.0;

    // 写入参数 Modbus 地址
    public const ushort CarJogSpeedAddress = 1000;
    public const ushort CarPositionTargetAddress = 1002;
    public const ushort CarPositionSpeedAddress = 1004;
    public const ushort CarPositionStartAddress = (ushort)(104 + MAddressOffset);
    public const ushort CarJogForwardAddress = (ushort)(100 + MAddressOffset);
    public const ushort CarJogBackwardAddress = (ushort)(101 + MAddressOffset);
    public const ushort CarHomeAddress = (ushort)(102 + MAddressOffset);
    public const ushort CarFaultResetAddress = (ushort)(109 + MAddressOffset);

    public const ushort WheelLongitudinalJogSpeedAddress = 1040;
    public const ushort WheelLongitudinalPositionTargetAddress = 1042;
    public const ushort WheelLongitudinalPositionSpeedAddress = 1044;
    public const ushort WheelLongitudinalPositionStartAddress = (ushort)(304 + MAddressOffset);
    public const ushort WheelLongitudinalJogDownAddress = (ushort)(300 + MAddressOffset);
    public const ushort WheelLongitudinalJogUpAddress = (ushort)(301 + MAddressOffset);
    public const ushort WheelLongitudinalHomeAddress = (ushort)(302 + MAddressOffset);
    public const ushort WheelLongitudinalFaultResetAddress = (ushort)(309 + MAddressOffset);

    public const ushort WheelLateralJogSpeedAddress = 1060;
    public const ushort WheelLateralPositionTargetAddress = 1062;
    public const ushort WheelLateralPositionSpeedAddress = 1064;
    public const ushort WheelLateralPositionStartAddress = (ushort)(404 + MAddressOffset);
    public const ushort WheelLateralJogLeftAddress = (ushort)(400 + MAddressOffset);
    public const ushort WheelLateralJogRightAddress = (ushort)(401 + MAddressOffset);
    public const ushort WheelLateralHomeAddress = (ushort)(402 + MAddressOffset);
    public const ushort WheelLateralFaultResetAddress = (ushort)(409 + MAddressOffset);

    public const ushort WheelAngleJogSpeedAddress = 1080;
    public const ushort WheelAnglePositionTargetAddress = 1082;
    public const ushort WheelAnglePositionSpeedAddress = 1084;
    public const ushort WheelAnglePositionStartAddress = (ushort)(504 + MAddressOffset);
    public const ushort WheelAngleJogReverseAddress = (ushort)(500 + MAddressOffset);
    public const ushort WheelAngleJogForwardAddress = (ushort)(501 + MAddressOffset);
    public const ushort WheelAngleHomeAddress = (ushort)(502 + MAddressOffset);
    public const ushort WheelAngleFaultResetAddress = (ushort)(509 + MAddressOffset);

    public const ushort ProfilerJogSpeedAddress = 1100;
    public const ushort ProfilerPositionTargetAddress = 1102;
    public const ushort ProfilerPositionSpeedAddress = 1104;
    public const ushort ProfilerPositionStartAddress = (ushort)(604 + MAddressOffset);
    public const ushort ProfilerJogDownAddress = (ushort)(600 + MAddressOffset);
    public const ushort ProfilerJogUpAddress = (ushort)(601 + MAddressOffset); 
    public const ushort ProfilerHomeAddress = (ushort)(602 + MAddressOffset);
    public const ushort ProfilerFaultResetAddress = (ushort)(609 + MAddressOffset);

    public const ushort WheelRotationSpeedAddress = 1150;
    public const ushort WheelRunCommandAddress = 1152;
    public const ushort WheelRunExecutionAddress = (ushort)(710 + MAddressOffset);
    public const ushort MeasurementStartPositionAddress = 1140;
    public const ushort MeasurementEndPositionAddress = 1142;
    public const ushort GrindingStartPositionAddress = 1180;
    public const ushort GrindingEndPositionAddress = 1182;
    public const ushort MeasurementMotionStartAddress = (ushort)(31 + MAddressOffset);
    public const ushort MeasurementProfileCaptureStartAddress = (ushort)(60 + MAddressOffset);
    public const ushort MeasurementPositionCompletedAddress = (ushort)(61 + MAddressOffset);
    public const ushort MeasurementCurrentProfileCompletedAddress = (ushort)(62 + MAddressOffset);
    public const ushort GrindingMotionStartAddress = (ushort)(32 + MAddressOffset);
    public const ushort GrindingTimesResultStartAddress = 1800;
    public const ushort GrindingTimesResultAddressStep = 2;

    public const ushort GrindingRunningAddress = 66 + MAddressOffset;
    public const ushort MeasurementRunningAddress = 65 + MAddressOffset;
    public const ushort ProfilerMeasurementPositionAddress = 1144;
    public const ushort ProfilerAvoidancePositionAddress = 1146;
    public const ushort CarMeasurementSpeedAddress = 1132;
    public const ushort BatteryLevelAddress = 2800;
    public const ushort PressureSensor1Address = 2802;
    public const ushort PressureSensor2Address = 2804;
    public const string ProfilerMeasurementPositionName = "廓形仪测量位置";
    public const string ProfilerAvoidancePositionName = "廓形仪测量回避位";
    public const string CarMeasurementSpeedName = "小车测量行走速度";
    public const string BatteryLevelName = "电池电量";
    public const string PressureSensor1Name = "压力传感器1实时值";
    public const string PressureSensor2Name = "压力传感器2实时值";
    public const string MeasurementRunningName = "廓形测量运行标志";
    public const string UnitPercent = "%";
    public const string UnitNewton = "N";
    public const double ProfilerMeasurementPositionScale = 10000.0;
    public const double ProfilerAvoidancePositionScale = 10000.0;
    public const double CarMeasurementSpeedScale = 1000.0;
    public const int DashboardPollIntervalMs = 500;
    public const int GrindingStatusPollIntervalMs = 500;
    public static GrindingParameterDefinition GrindingAngleParameter { get; } =
        new("Angle", "砂轮打磨角度", 1200, 10000m, UnitDegree);
    public static IReadOnlyList<GrindingParameterDefinition> GrindingBasicParameters { get; } =
        System.Array.AsReadOnly(new GrindingParameterDefinition[]
        {
            new("Start", GrindingStartPositionName, GrindingStartPositionAddress, 100000m, UnitMeter),
            new("End", GrindingEndPositionName, GrindingEndPositionAddress, 100000m, UnitMeter),
            new("Smooth", "打磨平滑距离", 1188, 100000m, UnitMeter, NonNegative: true),
            new("Speed", "小车打磨行走速度", 1134, 1000m, UnitMeterPerMinute, Positive: true)
        });
    public static IReadOnlyList<GrindingParameterDefinition> GrindingAngleParameters { get; } =
        System.Array.AsReadOnly(new GrindingParameterDefinition[]
        {
            new("Longitudinal", "纵向初始定位", 1300, 10000m, UnitMillimeter),
            new("Lateral", "横向初始定位", 1400, 10000m, UnitMillimeter),
            new("Torque", "目标转矩", 1700, 1m, "%", IsInt16: true, IsInteger: true, NonNegative: true),
            new("Reposition", "再进定位距离", 1900, 10000m, UnitMillimeter, NonNegative: true),
            new("Retreat", "进给回退距离", 2000, 10000m, UnitMillimeter, NonNegative: true),
            new("Feed", "单次进给距离", 2100, 10000m, UnitMillimeter, NonNegative: true),
            new("PositionPressure1", "对位压力1", 2200, 1m, "N", IsInteger: true, NonNegative: true),
            new("PositionPressure2", "对位压力2", 2300, 1m, "N", IsInteger: true, NonNegative: true),
            new("GrindingPressure1", "打磨压力1", 2400, 1m, "N", IsInteger: true, NonNegative: true),
            new("GrindingPressure2", "打磨压力2", 2500, 1m, "N", IsInteger: true, NonNegative: true)
        });

    // 写入参数比例
    public const double CarJogSpeedScale = 1000.0;
    public const double CarPositionTargetAddressScale = 100000.0;
    public const double CarPositionSpeedScale = 1000.0;

    public const double WheelLongitudinalJogSpeedScale = 100.0;
    public const double WheelLongitudinalPositionTargetAddressScale = 10000.0;
    public const double WheelLongitudinalPositionSpeedScale = 100.0;

    public const double WheelLateralJogSpeedScale = 100.0;
    public const double WheelLateralPositionTargetAddressScale = 10000.0;
    public const double WheelLateralPositionSpeedScale = 100.0;

    public const double WheelAngleJogSpeedScale = 100.0;
    public const double WheelAnglePositionTargetAddressScale = 10000.0;
    public const double WheelAnglePositionSpeedScale = 100.0;

    public const double ProfilerJogSpeedScale = 100.0;
    public const double ProfilerPositionTargetAddressScale = 10000.0;
    public const double ProfilerPositionSpeedScale = 100.0;

    public const double WheelRotationSpeedScale = 1.0 / 0.3;
    public const double WheelRunCommandScale = 1.0;
    public const double MeasurementStartPositionScale = 100000.0;
    public const double MeasurementEndPositionScale = 100000.0;
    public const double GrindingStartPositionScale = 100000.0;
    public const double GrindingEndPositionScale = 100000.0;
    public const double BoolScale = 1.0;

    public static IReadOnlyList<string> ReadOnlyParameterNames { get; } = new List<string>
    {
        CarCurrentPositionName,
        WheelLongitudinalCurrentPositionName,
        WheelLateralCurrentPositionName,
        WheelAngleCurrentPositionName,
        ProfilerCurrentPositionName
    };

    public static IReadOnlyList<string> WritableParameterNames { get; } = new List<string>
    {
        CarJogSpeedName,
        CarPositionTargetAddressName,
        CarPositionSpeedName,
        CarPositionStartName,
        CarJogForwardName,
        CarJogBackwardName,
        CarHomeName,
        CarFaultResetName,

        WheelLongitudinalJogSpeedName,
        WheelLongitudinalPositionTargetAddressName,
        WheelLongitudinalPositionSpeedName,
        WheelLongitudinalPositionStartName,
        WheelLongitudinalJogDownName,
        WheelLongitudinalJogUpName,
        WheelLongitudinalHomeName,
        WheelLongitudinalFaultResetName,

        WheelLateralJogSpeedName,
        WheelLateralPositionTargetAddressName,
        WheelLateralPositionSpeedName,
        WheelLateralPositionStartName,
        WheelLateralJogLeftName,
        WheelLateralJogRightName,
        WheelLateralHomeName,
        WheelLateralFaultResetName,

        WheelAngleJogSpeedName,
        WheelAnglePositionTargetAddressName,
        WheelAnglePositionSpeedName,
        WheelAnglePositionStartName,
        WheelAngleJogReverseName,
        WheelAngleJogForwardName,
        WheelAngleHomeName,
        WheelAngleFaultResetName,

        ProfilerJogSpeedName,
        ProfilerPositionTargetAddressName,
        ProfilerPositionSpeedName,
        ProfilerPositionStartName,
        ProfilerJogDownName,
        ProfilerJogUpName,
        ProfilerHomeName,
        ProfilerFaultResetName,

        WheelRotationSpeedName,
        WheelRunCommandName,
        WheelRunExecutionName
    };

    public static IReadOnlyDictionary<string, string> ParameterUnits { get; } = new Dictionary<string, string>
    {
        // 小车
        [CarCurrentPositionName] = UnitMeter,
        [CarJogSpeedName] = UnitMeterPerMinute,
        [CarPositionTargetAddressName] = UnitMeter,
        [CarPositionSpeedName] = UnitMeterPerMinute,
        [CarPositionStartName] = string.Empty,
        [CarJogForwardName] = string.Empty,
        [CarJogBackwardName] = string.Empty,
        [CarHomeName] = string.Empty,
        [CarFaultResetName] = string.Empty,

        // 砂轮纵向
        [WheelLongitudinalCurrentPositionName] = UnitMillimeter,
        [WheelLongitudinalJogSpeedName] = UnitMillimeterPerMinute,
        [WheelLongitudinalPositionTargetAddressName] = UnitMillimeter,
        [WheelLongitudinalPositionSpeedName] = UnitMillimeterPerMinute,
        [WheelLongitudinalPositionStartName] = string.Empty,
        [WheelLongitudinalJogDownName] = string.Empty,
        [WheelLongitudinalJogUpName] = string.Empty,
        [WheelLongitudinalHomeName] = string.Empty,
        [WheelLongitudinalFaultResetName] = string.Empty,

        // 砂轮横向
        [WheelLateralCurrentPositionName] = UnitMillimeter,
        [WheelLateralJogSpeedName] = UnitMillimeterPerMinute,
        [WheelLateralPositionTargetAddressName] = UnitMillimeter,
        [WheelLateralPositionSpeedName] = UnitMillimeterPerMinute,
        [WheelLateralPositionStartName] = string.Empty,
        [WheelLateralJogLeftName] = string.Empty,
        [WheelLateralJogRightName] = string.Empty,
        [WheelLateralHomeName] = string.Empty,
        [WheelLateralFaultResetName] = string.Empty,

        // 砂轮角度
        [WheelAngleCurrentPositionName] = UnitDegree,
        [WheelAngleJogSpeedName] = UnitDegreePerMinute,
        [WheelAnglePositionTargetAddressName] = UnitDegree,
        [WheelAnglePositionSpeedName] = UnitDegreePerMinute,
        [WheelAnglePositionStartName] = string.Empty,
        [WheelAngleJogReverseName] = string.Empty,
        [WheelAngleJogForwardName] = string.Empty,
        [WheelAngleHomeName] = string.Empty,
        [WheelAngleFaultResetName] = string.Empty,

        // 廓形仪
        [ProfilerCurrentPositionName] = UnitMillimeter,
        [ProfilerJogSpeedName] = UnitMillimeterPerMinute,
        [ProfilerPositionTargetAddressName] = UnitMillimeter,
        [ProfilerPositionSpeedName] = UnitMillimeterPerMinute,
        [ProfilerPositionStartName] = string.Empty,
        [ProfilerJogDownName] = string.Empty,
        [ProfilerJogUpName] = string.Empty,
        [ProfilerHomeName] = string.Empty,
        [ProfilerFaultResetName] = string.Empty,

        // 砂轮旋转速度 / 运行控制
        [WheelRotationSpeedName] = UnitRadianPerMinute,
        [WheelRunCommandName] = string.Empty,
        [WheelRunExecutionName] = string.Empty
    };

    public static IReadOnlyList<int> MeasurementGrindingAngles { get; } = new List<int>
    {
        -35, -20, -15, -10, -5, -2, 0, 2, 5, 10, 15, 25, 35, 45, 55, 65, 75, 83, 90
    };

    public static IReadOnlyDictionary<int, ushort> MeasurementGrindingTimesAddresses { get; } =
        BuildMeasurementGrindingTimesAddressMap();

    private static IReadOnlyDictionary<int, ushort> BuildMeasurementGrindingTimesAddressMap()
    {
        var addressMap = new Dictionary<int, ushort>(MeasurementGrindingAngles.Count);
        for (int index = 0; index < MeasurementGrindingAngles.Count; index++)
        {
            int angle = MeasurementGrindingAngles[index];
            addressMap[angle] = (ushort)(GrindingTimesResultStartAddress + (index * GrindingTimesResultAddressStep));
        }

        return addressMap;
    }
}
