namespace GrindCar.Model;

/// <summary>
/// 电机参数模型（与 PLC 地址含义对应）
/// </summary>
public class Motor
{
    // 小车当前位置
    public int CarCurrentPosition { get; set; }

    // 小车点动速度
    public int CarJogSpeed { get; set; }

    // 小车定位运行地址
    public int CarPositionTargetAddress { get; set; }

    // 小车定位运行速度
    public int CarPositionSpeed { get; set; }

    // 小车定位运行启动
    public bool CarPositionStart { get; set; }

    // 小车点动前进
    public bool CarJogForward { get; set; }

    // 小车点动后退
    public bool CarJogBackward { get; set; }

    // 小车定位回零
    public bool CarHome { get; set; }

    // 小车故障复位
    public bool CarFaultReset { get; set; }

    // 砂轮纵向当前位置
    public int WheelLongitudinalCurrentPosition { get; set; }

    // 砂轮纵向点动速度
    public int WheelLongitudinalJogSpeed { get; set; }

    // 砂轮纵向定位运行地址
    public int WheelLongitudinalPositionTargetAddress { get; set; }

    // 砂轮纵向定位运行速度
    public int WheelLongitudinalPositionSpeed { get; set; }

    // 砂轮纵向定位运行启动
    public bool WheelLongitudinalPositionStart { get; set; }

    // 砂轮纵向点动下行
    public bool WheelLongitudinalJogDown { get; set; }

    // 砂轮纵向点动上行
    public bool WheelLongitudinalJogUp { get; set; }

    // 砂轮纵向定位回零
    public bool WheelLongitudinalHome { get; set; }

    // 砂轮纵向故障复位
    public bool WheelLongitudinalFaultReset { get; set; }

    // 砂轮横向当前位置
    public int WheelLateralCurrentPosition { get; set; }

    // 砂轮横向点动速度
    public int WheelLateralJogSpeed { get; set; }

    // 砂轮横向定位运行地址
    public int WheelLateralPositionTargetAddress { get; set; }

    // 砂轮横向定位运行速度
    public int WheelLateralPositionSpeed { get; set; }

    // 砂轮横向定位运行启动
    public bool WheelLateralPositionStart { get; set; }

    // 砂轮横向点动左行
    public bool WheelLateralJogLeft { get; set; }

    // 砂轮横向点动右行
    public bool WheelLateralJogRight { get; set; }

    // 砂轮横向定位回零
    public bool WheelLateralHome { get; set; }

    // 砂轮横向故障复位
    public bool WheelLateralFaultReset { get; set; }

    // 砂轮角度当前位置
    public int WheelAngleCurrentPosition { get; set; }

    // 砂轮角度点动速度
    public int WheelAngleJogSpeed { get; set; }

    // 砂轮角度定位运行地址
    public int WheelAnglePositionTargetAddress { get; set; }

    // 砂轮角度定位运行速度
    public int WheelAnglePositionSpeed { get; set; }

    // 砂轮角度定位运行启动
    public bool WheelAnglePositionStart { get; set; }

    // 砂轮角度点动逆行
    public bool WheelAngleJogReverse { get; set; }

    // 砂轮角度点动顺行
    public bool WheelAngleJogForward { get; set; }

    // 砂轮角度定位回零
    public bool WheelAngleHome { get; set; }

    // 砂轮角度故障复位
    public bool WheelAngleFaultReset { get; set; }

    // 廓形仪当前位置
    public int ProfilerCurrentPosition { get; set; }

    // 廓形仪点动速度
    public int ProfilerJogSpeed { get; set; }

    // 廓形仪定位运行地址
    public int ProfilerPositionTargetAddress { get; set; }

    // 廓形仪定位运行速度
    public int ProfilerPositionSpeed { get; set; }

    // 廓形仪定位运行启动
    public bool ProfilerPositionStart { get; set; }

    // 廓形仪点动下行
    public bool ProfilerJogDown { get; set; }

    // 廓形仪点动上行
    public bool ProfilerJogUp { get; set; }

    // 廓形仪定位回零
    public bool ProfilerHome { get; set; }

    // 廓形仪故障复位
    public bool ProfilerFaultReset { get; set; }

    // 砂轮旋转速度
    public short WheelRotationSpeed { get; set; }

    // 砂轮质量控制
    public short WheelQualityControl { get; set; }
}

