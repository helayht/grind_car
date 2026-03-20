using System.Collections.Generic;
using GrindCar.Models.Rail;

namespace GrindCar.Services.Rail;

public interface IRailProfileFittingService
{
    RailProfileFitResult Fit(IReadOnlyCollection<RailProfilePoint> points);
}
