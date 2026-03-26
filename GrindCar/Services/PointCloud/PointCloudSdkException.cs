using System;

namespace GrindCar.Services.PointCloud;

public sealed class PointCloudSdkException : Exception
{
    public PointCloudSdkException(string message)
        : base(message)
    {
    }

    public PointCloudSdkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
