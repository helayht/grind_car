using System;
using System.IO;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.ViewModels;
using Xunit;

namespace GrindCar.Tests;

public class ProfileRegistrationViewModelTests
{
    [Fact]
    public void TranslationProperties_WhenOutOfRange_AreClamped()
    {
        ProfileRegistrationViewModel viewModel = CreateViewModel();

        viewModel.PendingLeftDx = 250.0;
        viewModel.PendingLeftDy = -250.0;
        viewModel.PendingRightDx = 201.0;
        viewModel.PendingRightDy = -201.0;

        Assert.Equal(200.0, viewModel.PendingLeftDx, 6);
        Assert.Equal(-200.0, viewModel.PendingLeftDy, 6);
        Assert.Equal(200.0, viewModel.PendingRightDx, 6);
        Assert.Equal(-200.0, viewModel.PendingRightDy, 6);
    }

    [Fact]
    public void RotationProperties_WhenOutOfRange_AreClamped()
    {
        ProfileRegistrationViewModel viewModel = CreateViewModel();

        viewModel.PendingLeftRotationDegrees = 400.0;
        viewModel.PendingRightRotationDegrees = -400.0;

        Assert.Equal(360.0, viewModel.PendingLeftRotationDegrees, 6);
        Assert.Equal(-360.0, viewModel.PendingRightRotationDegrees, 6);
    }

    [Fact]
    public void TransformProperties_WhenValid_AreStored()
    {
        ProfileRegistrationViewModel viewModel = CreateViewModel();

        viewModel.PendingLeftDx = 1.25;
        viewModel.PendingLeftDy = -2.5;
        viewModel.PendingLeftRotationDegrees = 120.75;
        viewModel.PendingRightDx = -3.5;
        viewModel.PendingRightDy = 4.5;
        viewModel.PendingRightRotationDegrees = -220.25;

        Assert.Equal(1.25, viewModel.PendingLeftDx, 6);
        Assert.Equal(-2.5, viewModel.PendingLeftDy, 6);
        Assert.Equal(120.75, viewModel.PendingLeftRotationDegrees, 6);
        Assert.Equal(-3.5, viewModel.PendingRightDx, 6);
        Assert.Equal(4.5, viewModel.PendingRightDy, 6);
        Assert.Equal(-220.25, viewModel.PendingRightRotationDegrees, 6);
    }

    [Fact]
    public void PendingChanges_DoNotUpdateAppliedParametersUntilApply()
    {
        ProfileRegistrationViewModel viewModel = CreateViewModel();

        viewModel.PendingLeftDx = 1.0;
        viewModel.PendingLeftDy = 2.0;
        viewModel.PendingLeftRotationDegrees = 30.0;
        viewModel.PendingLeftIsMirrored = true;
        viewModel.PendingRightDx = -1.0;
        viewModel.PendingRightDy = -2.0;
        viewModel.PendingRightRotationDegrees = -30.0;
        viewModel.PendingRightIsMirrored = true;

        Assert.Equal(0.0, viewModel.AppliedLeftDx, 6);
        Assert.Equal(0.0, viewModel.AppliedLeftDy, 6);
        Assert.Equal(0.0, viewModel.AppliedLeftRotationDegrees, 6);
        Assert.False(viewModel.AppliedLeftIsMirrored);
        Assert.Equal(0.0, viewModel.AppliedRightDx, 6);
        Assert.Equal(0.0, viewModel.AppliedRightDy, 6);
        Assert.Equal(0.0, viewModel.AppliedRightRotationDegrees, 6);
        Assert.False(viewModel.AppliedRightIsMirrored);

        viewModel.ApplyPendingParameters();

        Assert.Equal(1.0, viewModel.AppliedLeftDx, 6);
        Assert.Equal(2.0, viewModel.AppliedLeftDy, 6);
        Assert.Equal(30.0, viewModel.AppliedLeftRotationDegrees, 6);
        Assert.True(viewModel.AppliedLeftIsMirrored);
        Assert.Equal(-1.0, viewModel.AppliedRightDx, 6);
        Assert.Equal(-2.0, viewModel.AppliedRightDy, 6);
        Assert.Equal(-30.0, viewModel.AppliedRightRotationDegrees, 6);
        Assert.True(viewModel.AppliedRightIsMirrored);
    }

    [Fact]
    public void SaveSettings_SavesAppliedParametersOnly()
    {
        string filePath = BuildTempFilePath();
        ProfileRegistrationViewModel viewModel = CreateViewModel(filePath);
        viewModel.PendingLeftDx = 1.0;
        viewModel.PendingLeftDy = 2.0;
        viewModel.PendingLeftRotationDegrees = 30.0;
        viewModel.PendingLeftIsMirrored = true;
        viewModel.PendingRightDx = -1.0;
        viewModel.PendingRightDy = -2.0;
        viewModel.PendingRightRotationDegrees = -30.0;
        viewModel.PendingRightIsMirrored = true;
        viewModel.ApplyPendingParameters();
        viewModel.PendingLeftDx = 9.0;
        viewModel.PendingLeftIsMirrored = false;
        viewModel.PendingRightDx = -9.0;
        viewModel.PendingRightIsMirrored = false;

        viewModel.SaveSettings();

        ProfileRegistrationSettings saved = new ProfileRegistrationSettingsStore(filePath).LoadRequired();
        Assert.Equal(1.0, saved.Left!.Dx, 6);
        Assert.Equal(2.0, saved.Left.Dy, 6);
        Assert.Equal(30.0, saved.Left.RotationDegrees, 6);
        Assert.True(saved.Left.IsMirrored);
        Assert.Equal(-1.0, saved.Right!.Dx, 6);
        Assert.Equal(-2.0, saved.Right.Dy, 6);
        Assert.Equal(-30.0, saved.Right.RotationDegrees, 6);
        Assert.True(saved.Right.IsMirrored);
    }

    private static ProfileRegistrationViewModel CreateViewModel(string? filePath = null)
    {
        return new ProfileRegistrationViewModel(
            new ProfileRegistrationSettingsStore(filePath ?? BuildTempFilePath()),
            new ProfileRegistrationTransformService());
    }

    private static string BuildTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "point-cloud-profile-registration.json");
    }
}
