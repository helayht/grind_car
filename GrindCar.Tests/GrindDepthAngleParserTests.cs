using System;
using GrindCar.Services.Rail.Debug;
using Xunit;

namespace GrindCar.Tests;

public class GrindDepthAngleParserTests
{
    [Fact]
    public void ParseAngles_WithMixedSeparators_ReturnsExpectedSequence()
    {
        string input = "-20，-10 0\n5\t15";

        var result = GrindDepthAngleParser.ParseAngles(input);

        Assert.Equal(new[] { -20, -10, 0, 5, 15 }, result);
    }

    [Fact]
    public void ParseAngles_WithInvalidToken_Throws()
    {
        string input = "-20,abc,10";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GrindDepthAngleParser.ParseAngles(input));

        Assert.Contains("无法解析角度值", exception.Message);
    }
}