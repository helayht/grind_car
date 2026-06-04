using System;
using System.Globalization;
using System.Threading;
using GrindCar.Services.Measurement;
using Xunit;

namespace GrindCar.Tests;

public class MeasurementInputParserTests
{
    [Fact]
    public void ParsePosition_UsesCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            double result = MeasurementInputParser.ParsePosition("12,5", "位置");

            Assert.Equal(12.5, result, 6);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void ParsePosition_FallsBackToInvariantCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            double result = MeasurementInputParser.ParsePosition("12.5", "位置");

            Assert.Equal(12.5, result, 6);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void ParsePosition_EmptyInput_ThrowsExpectedMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => MeasurementInputParser.ParsePosition(" ", "位置"));

        Assert.Equal("位置不能为空。", exception.Message);
    }

    [Fact]
    public void ParsePosition_InvalidInput_ThrowsExpectedMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => MeasurementInputParser.ParsePosition("abc", "位置"));

        Assert.Equal("位置格式无效。", exception.Message);
    }
}
