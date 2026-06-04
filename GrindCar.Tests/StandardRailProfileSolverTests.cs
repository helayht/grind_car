using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using Xunit;

namespace GrindCar.Tests;

public class StandardRailProfileSolverTests
{
    [Fact]
    public void GetRepresentativeB_UsesStandardCoordinateSystemWithoutRecentering()
    {
        var points = new[]
        {
            new RailProfilePoint(10.0, 5.0),
            new RailProfilePoint(20.0, 100.0)
        };

        double result = StandardRailProfileSolver.GetRepresentativeB(0.0, points);

        Assert.Equal(100.0, result, 6);
    }
}
