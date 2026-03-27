using System;

namespace GrindCar.Services.Rail;

public sealed class RepresentativeProfileExtractionException : Exception
{
    public RepresentativeProfileExtractionException(string message)
        : base(message)
    {
    }

    public RepresentativeProfileExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
