using System;
using System.Collections.Generic;
using System.Windows;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Debug;
using Xunit;

namespace GrindCar.Tests;

public class ProfileRegistrationPlotMapperTests
{
    [Fact]
    public void MapToScreen_UsesSameScaleForXAndY()
    {
        var mapper = new ProfileRegistrationPlotMapper(plotPadding: 10.0);
        var points = new List<RailProfilePoint>
        {
            new(0.0, 0.0),
            new(10.0, 10.0)
        };

        mapper.Update(points, plotWidth: 220.0, plotHeight: 220.0);

        Point origin = mapper.MapToScreen(new RailProfilePoint(0.0, 0.0));
        Point xUnit = mapper.MapToScreen(new RailProfilePoint(1.0, 0.0));
        Point yUnit = mapper.MapToScreen(new RailProfilePoint(0.0, 1.0));

        Assert.Equal(xUnit.X - origin.X, origin.Y - yUnit.Y, 6);
    }

    [Fact]
    public void MapToScreen_WithWideBounds_CentersVertically()
    {
        var mapper = new ProfileRegistrationPlotMapper(plotPadding: 10.0);
        var points = new List<RailProfilePoint>
        {
            new(0.0, 0.0),
            new(100.0, 10.0)
        };

        mapper.Update(points, plotWidth: 220.0, plotHeight: 220.0);

        Point minPoint = mapper.MapToScreen(new RailProfilePoint(0.0, 0.0));
        Point maxPoint = mapper.MapToScreen(new RailProfilePoint(100.0, 10.0));
        double topWhitespace = maxPoint.Y - 10.0;
        double bottomWhitespace = 210.0 - minPoint.Y;

        Assert.True(topWhitespace > 0.0);
        Assert.True(bottomWhitespace > 0.0);
        Assert.True(Math.Abs(topWhitespace - bottomWhitespace) < 1.0);
    }

    [Fact]
    public void MapToScreen_WithTallBounds_CentersHorizontally()
    {
        var mapper = new ProfileRegistrationPlotMapper(plotPadding: 10.0);
        var points = new List<RailProfilePoint>
        {
            new(0.0, 0.0),
            new(10.0, 100.0)
        };

        mapper.Update(points, plotWidth: 220.0, plotHeight: 220.0);

        Point minPoint = mapper.MapToScreen(new RailProfilePoint(0.0, 0.0));
        Point maxPoint = mapper.MapToScreen(new RailProfilePoint(10.0, 100.0));
        double leftWhitespace = minPoint.X - 10.0;
        double rightWhitespace = 210.0 - maxPoint.X;

        Assert.True(leftWhitespace > 0.0);
        Assert.True(rightWhitespace > 0.0);
        Assert.True(Math.Abs(leftWhitespace - rightWhitespace) < 1.0);
    }
}
