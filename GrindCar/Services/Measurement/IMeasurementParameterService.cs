using System.Threading.Tasks;

namespace GrindCar.Services.Measurement;

/// <summary>
/// 测量参数写入服务。
/// </summary>
public interface IMeasurementParameterService
{
    Task WriteMeasurementRangeAsync(string ipAddress, int port, double startPosition, double endPosition);

    Task StartMeasurementMotionAsync(string ipAddress, int port);
}