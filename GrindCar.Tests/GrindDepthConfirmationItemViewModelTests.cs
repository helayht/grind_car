using System;
using GrindCar.Services.Measurement;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class GrindDepthConfirmationItemViewModelTests
{
    [Fact]
    public void CalculateGrindingTimes_WithPositiveDepth_RoundsUpByDepthStep()
    {
        int grindingTimes = MeasurementParameterService.CalculateGrindingTimes(0.051);

        Assert.Equal(2, grindingTimes);
    }

    [Fact]
    public void CalculateGrindingTimes_WithZeroDepth_ReturnsZero()
    {
        int grindingTimes = MeasurementParameterService.CalculateGrindingTimes(0.0);

        Assert.Equal(0, grindingTimes);
    }

    [Fact]
    public void ConfirmedDepthText_WhenModified_RecalculatesGrindingTimes()
    {
        var item = new GrindDepthConfirmationItemViewModel(new MeasurementGrindingTimesResult(10, 0.05, 1));

        item.ConfirmedDepthText = "0.11";

        Assert.False(item.HasError);
        Assert.Equal(0.11, item.ConfirmedDepth, 6);
        Assert.Equal(3, item.GrindingTimes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-0.01")]
    public void ValidateConfirmedDepth_WithInvalidInput_ReturnsFalse(string input)
    {
        var item = new GrindDepthConfirmationItemViewModel(new MeasurementGrindingTimesResult(10, 0.05, 1));

        item.ConfirmedDepthText = input;

        Assert.True(item.HasError);
        Assert.False(item.ValidateConfirmedDepth());
        Assert.NotEmpty(item.ErrorMessage);
    }

    [Fact]
    public void ToConfirmedResult_WithValidInput_ReturnsEditedDepthAndTimes()
    {
        var item = new GrindDepthConfirmationItemViewModel(new MeasurementGrindingTimesResult(10, 0.05, 1))
        {
            ConfirmedDepthText = "0.101"
        };

        MeasurementGrindingTimesResult result = item.ToConfirmedResult();

        Assert.Equal(10, result.Angle);
        Assert.Equal(0.101, result.AverageGrindDepth, 6);
        Assert.Equal(3, result.GrindingTimes);
    }

    [Fact]
    public void CalculateGrindingTimes_WithNegativeDepth_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            MeasurementParameterService.CalculateGrindingTimes(-0.01));

        Assert.Contains("不能小于 0", exception.Message);
    }
}
