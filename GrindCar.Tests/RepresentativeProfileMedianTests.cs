using System;
using System.Collections.Generic;
using System.Reflection;
using GrindCar.Services.Rail.Processing;
using Xunit;

namespace GrindCar.Tests;

public class RepresentativeProfileMedianTests
{
    [Fact]
    public void Median_OddValues_ReturnsMiddleValue()
    {
        double result = InvokeMedian(new[] { 5.0, 1.0, 3.0 });

        Assert.Equal(3.0, result, 6);
    }

    [Fact]
    public void Median_EvenValues_ReturnsAverageOfMiddleValues()
    {
        double result = InvokeMedian(new[] { 10.0, 2.0, 8.0, 4.0 });

        Assert.Equal(6.0, result, 6);
    }

    [Fact]
    public void Median_UnorderedValues_MatchesSortedMedian()
    {
        double result = InvokeMedian(new[] { 12.0, -3.0, 7.0, 7.0, 1.0, 20.0 });

        Assert.Equal(7.0, result, 6);
    }

    [Fact]
    public void Median_EmptyValues_ThrowsArgumentException()
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeMedian(Array.Empty<double>()));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private static double InvokeMedian(IReadOnlyList<double> values)
    {
        MethodInfo? method = typeof(RepresentativeProfilePointProcessor).GetMethod(
            "Median",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        object? result = method.Invoke(null, new object[] { values });
        return Assert.IsType<double>(result);
    }
}
