using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GrindCar.Definitions;
using GrindCar.Models.Grinding;

namespace GrindCar.Services.Grinding;

public interface IGrindingParameterService
{
    Task WriteAllAsync(GrindingSettings settings, CancellationToken cancellationToken);
    Task WriteAngleAsync(GrindingAngleSettings settings, CancellationToken cancellationToken);
    Task<bool> ReadRunningAsync(CancellationToken cancellationToken);
}

public sealed record GrindingRegisterWrite(string Name, ushort Address, int Value, bool IsInt16);

public static class GrindingSettingsValidator
{
    public static int ToRaw(GrindingParameterDefinition definition, decimal value)
    {
        if ((definition.NonNegative && value < 0) || (definition.Positive && value <= 0))
            throw new InvalidOperationException($"{definition.Name}必须{(definition.Positive ? "大于" : "不小于")}0。");
        if (definition.IsInteger && decimal.Truncate(value) != value)
            throw new InvalidOperationException($"{definition.Name}必须是整数。");
        try
        {
            decimal raw = decimal.Round(checked(value * definition.Scale), 0, MidpointRounding.ToEven);
            return definition.IsInt16 ? checked((short)raw) : checked((int)raw);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException($"{definition.Name}超出{(definition.IsInt16 ? "Int16" : "Int32")}范围。", ex);
        }
    }

    private static List<GrindingRegisterWrite> BuildValues(
        IReadOnlyList<GrindingParameterDefinition> definitions, Dictionary<string, decimal>? values, int index)
    {
        if (values == null || values.Count != definitions.Count)
            throw new InvalidOperationException("参数数量不正确，请完整填写设置。");
        var writes = new List<GrindingRegisterWrite>();
        foreach (GrindingParameterDefinition definition in definitions)
        {
            if (!values.TryGetValue(definition.Key, out decimal value))
                throw new InvalidOperationException($"缺少参数：{definition.Name}。");
            writes.Add(new(definition.Name, definition.Address(index), ToRaw(definition, value), definition.IsInt16));
        }
        return writes;
    }

    public static List<GrindingRegisterWrite> BuildAngle(GrindingAngleSettings settings)
    {
        if (settings == null || settings.Number < 1 || settings.Number > MotorParameterDefinitions.MeasurementGrindingAngles.Count ||
            settings.Angle != MotorParameterDefinitions.MeasurementGrindingAngles[settings.Number - 1])
            throw new InvalidOperationException("角度序号或固定角度不匹配。");
        int index = settings.Number - 1;
        List<GrindingRegisterWrite> writes = BuildValues(MotorParameterDefinitions.GrindingAngleParameters, settings.Values, index);
        GrindingParameterDefinition angle = MotorParameterDefinitions.GrindingAngleParameter;
        writes.Insert(0, new(angle.Name, angle.Address(index), ToRaw(angle, settings.Angle), false));
        return writes;
    }

    public static List<GrindingRegisterWrite> BuildAll(GrindingSettings settings)
    {
        if (settings == null || settings.Version != GrindingSettings.CurrentVersion || settings.Angles == null ||
            settings.Angles.Count != MotorParameterDefinitions.MeasurementGrindingAngles.Count)
            throw new InvalidOperationException("打磨配置版本或角度数量不正确。");
        List<GrindingRegisterWrite> writes = BuildValues(MotorParameterDefinitions.GrindingBasicParameters, settings.Basic, 0);
        if (settings.Basic["Start"] > settings.Basic["End"])
            throw new InvalidOperationException("打磨起点不能大于终点。");
        for (int index = 0; index < settings.Angles.Count; index++)
        {
            if (settings.Angles[index] == null || settings.Angles[index].Number != index + 1)
                throw new InvalidOperationException("打磨配置的角度顺序不正确。");
            writes.AddRange(BuildAngle(settings.Angles[index]));
        }
        return writes;
    }
}

public sealed class GrindingParameterService : IGrindingParameterService
{
    private readonly Func<IPlcClient> _clientFactory;
    public GrindingParameterService(Func<IPlcClient> clientFactory) => _clientFactory = clientFactory;

    public Task WriteAllAsync(GrindingSettings settings, CancellationToken cancellationToken) =>
        WriteAsync(GrindingSettingsValidator.BuildAll(settings), cancellationToken);

    public Task WriteAngleAsync(GrindingAngleSettings settings, CancellationToken cancellationToken) =>
        WriteAsync(GrindingSettingsValidator.BuildAngle(settings), cancellationToken);

    public Task<bool> ReadRunningAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IPlcClient client = _clientFactory();
        return client.ReadSingleCoil(MotorParameterDefinitions.GrindingRunningAddress);
    }, cancellationToken);

    private Task WriteAsync(IReadOnlyList<GrindingRegisterWrite> writes, CancellationToken cancellationToken) => Task.Run(() =>
    {
        using IPlcClient client = _clientFactory();
        int completed = 0;
        foreach (GrindingRegisterWrite write in writes)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (client.ReadSingleCoil(MotorParameterDefinitions.GrindingRunningAddress))
                    throw new InvalidOperationException("打磨运行中，停止参数写入。");
                if (write.IsInt16) client.WriteInt16(write.Address, checked((short)write.Value));
                else client.WriteInt32(write.Address, write.Value);
                completed++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"写入失败：{write.Name} D{write.Address}；已完成 {completed}/{writes.Count} 项，PLC可能已部分写入，请重新核对。{ex.Message}", ex);
            }
        }
    }, cancellationToken);
}
