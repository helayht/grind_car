using System;
using GrindCar.Models.Rail;
using GrindCar.Services;
using GrindCar.Services.Rail.Core;
using Xunit;

namespace GrindCar.Tests;

public class StandardRailProfileSolverTests
{
    [Theory]
    [InlineData(RailProfileType.Kg60, -14.1291671483623)]
    [InlineData(RailProfileType.Kg50, -37.84)]
    public void GetMinimumProfileHeight_ProfileType_ReturnsExactGlobalMinimum(
        RailProfileType profileType,
        double expectedMinimumHeight)
    {
        double result = StandardRailProfileSolver.GetMinimumProfileHeight(profileType);

        Assert.Equal(expectedMinimumHeight, result, 10);
    }

    [Theory]
    [InlineData(-8.0, 8.0)]
    [InlineData(0.0, 20.0)]
    [InlineData(-12.0, 12.0)]
    public void GetMinimumProfileHeight_Range_ReturnsMinimumAtRangeEndpoint(
        double minimumX,
        double maximumX)
    {
        double expected = Math.Min(
            StandardRailProfileSolver.RailSurfaceFun(minimumX),
            StandardRailProfileSolver.RailSurfaceFun(maximumX));

        double result = StandardRailProfileSolver.GetMinimumProfileHeight(
            minimumX,
            maximumX);

        Assert.Equal(expected, result, 10);
    }

    [Fact]
    public void GetMinimumProfileHeight_RangeOutsideBoundary_ClampsToStandardDomain()
    {
        double result = StandardRailProfileSolver.GetMinimumProfileHeight(-100.0, -30.0);

        Assert.Equal(
            StandardRailProfileSolver.RailSurfaceFun(StandardRailProfileSolver.LeftBoundaryX),
            result,
            10);
    }

    [Theory]
    [InlineData(10.0, -10.0)]
    [InlineData(40.0, 50.0)]
    [InlineData(double.NaN, 10.0)]
    public void GetMinimumProfileHeight_InvalidRange_Throws(double minimumX, double maximumX)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            StandardRailProfileSolver.GetMinimumProfileHeight(minimumX, maximumX));
    }

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

    [Theory]
    [InlineData(90, 35.4)]
    [InlineData(-90, 35.4)]
    public void SolveNormalOffset_VerticalAngle_ReturnsFiniteBoundaryDistance(
        int angle,
        double expectedOffset)
    {
        double angleRadians = angle * Math.PI / 180.0;

        double result = StandardRailProfileSolver.SolveNormalOffset(angleRadians);

        Assert.True(double.IsFinite(result));
        Assert.Equal(expectedOffset, result, 9);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    public void CalculateGrindDepths_VerticalAngle_ReturnsFiniteHorizontalGap(int angle)
    {
        var profilePoints = new[]
        {
            new RailProfilePoint(-35.0, StandardRailProfileSolver.RailSurfaceFun(-35.0)),
            new RailProfilePoint(0.0, StandardRailProfileSolver.RailSurfaceFun(0.0)),
            new RailProfilePoint(35.0, StandardRailProfileSolver.RailSurfaceFun(35.0))
        };

        GrindDepthCalculationResult result = RailSurfaceService.CalculateGrindDepths(
            new[] { angle },
            profilePoints);

        Assert.True(double.IsFinite(result.Results[0].GrindDepth));
        Assert.Equal(0.4, result.Results[0].GrindDepth, 9);
    }
}
