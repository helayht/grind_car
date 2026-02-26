using System.Collections.Generic;

namespace GrindCar.Definitions;

public static class MotorParameterDefinitions
{
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
    public const string WheelQualityControlName = "砂轮质量控制";

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
    public const ushort CarPositionStartAddress = 104;
    public const ushort CarJogForwardAddress = 100;
    public const ushort CarJogBackwardAddress = 101;
    public const ushort CarHomeAddress = 102;
    public const ushort CarFaultResetAddress = 109;

    public const ushort WheelLongitudinalJogSpeedAddress = 1040;
    public const ushort WheelLongitudinalPositionTargetAddress = 1042;
    public const ushort WheelLongitudinalPositionSpeedAddress = 1044;
    public const ushort WheelLongitudinalPositionStartAddress = 304;
    public const ushort WheelLongitudinalJogDownAddress = 300;
    public const ushort WheelLongitudinalJogUpAddress = 301;
    public const ushort WheelLongitudinalHomeAddress = 302;
    public const ushort WheelLongitudinalFaultResetAddress = 309;

    public const ushort WheelLateralJogSpeedAddress = 1060;
    public const ushort WheelLateralPositionTargetAddress = 1062;
    public const ushort WheelLateralPositionSpeedAddress = 1064;
    public const ushort WheelLateralPositionStartAddress = 404;
    public const ushort WheelLateralJogLeftAddress = 400;
    public const ushort WheelLateralJogRightAddress = 401;
    public const ushort WheelLateralHomeAddress = 402;
    public const ushort WheelLateralFaultResetAddress = 409;

    public const ushort WheelAngleJogSpeedAddress = 1080;
    public const ushort WheelAnglePositionTargetAddress = 1082;
    public const ushort WheelAnglePositionSpeedAddress = 1084;
    public const ushort WheelAnglePositionStartAddress = 504;
    public const ushort WheelAngleJogReverseAddress = 500;
    public const ushort WheelAngleJogForwardAddress = 501;
    public const ushort WheelAngleHomeAddress = 502;
    public const ushort WheelAngleFaultResetAddress = 509;

    public const ushort ProfilerJogSpeedAddress = 1100;
    public const ushort ProfilerPositionTargetAddress = 1102;
    public const ushort ProfilerPositionSpeedAddress = 1104;
    public const ushort ProfilerPositionStartAddress = 604;
    public const ushort ProfilerJogDownAddress = 600;
    public const ushort ProfilerJogUpAddress = 601; 
    public const ushort ProfilerHomeAddress = 602;
    public const ushort ProfilerFaultResetAddress = 609;

    public const ushort WheelRotationSpeedAddress = 1150;
    public const ushort WheelQualityControlAddress = 1152;

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
    public const double WheelQualityControlScale = 1.0;
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
        WheelQualityControlName
    };
}
