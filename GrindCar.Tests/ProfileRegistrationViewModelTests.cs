using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail;
using GrindCar.Services.Rail.Core;
using GrindCar.Services.Rail.Processing;
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
        viewModel.PendingLeftXMin = -10.0;
        viewModel.PendingLeftXMax = 20.0;
        viewModel.PendingRightDx = -1.0;
        viewModel.PendingRightDy = -2.0;
        viewModel.PendingRightRotationDegrees = -30.0;
        viewModel.PendingRightIsMirrored = true;
        viewModel.PendingRightXMin = -30.0;
        viewModel.PendingRightXMax = 40.0;
        viewModel.ApplyPendingParameters();
        viewModel.PendingLeftDx = 9.0;
        viewModel.PendingLeftIsMirrored = false;
        viewModel.PendingLeftXMin = -90.0;
        viewModel.PendingLeftXMax = 90.0;
        viewModel.PendingRightDx = -9.0;
        viewModel.PendingRightIsMirrored = false;
        viewModel.PendingRightXMin = -80.0;
        viewModel.PendingRightXMax = 80.0;

        viewModel.SaveSettings();

        ProfileRegistrationSettings saved = new ProfileRegistrationSettingsStore(filePath).LoadRequired();
        Assert.Equal(1.0, saved.Left!.Dx, 6);
        Assert.Equal(2.0, saved.Left.Dy, 6);
        Assert.Equal(30.0, saved.Left.RotationDegrees, 6);
        Assert.True(saved.Left.IsMirrored);
        Assert.Equal(-10.0, saved.Left.XMin);
        Assert.Equal(20.0, saved.Left.XMax);
        Assert.Equal(-1.0, saved.Right!.Dx, 6);
        Assert.Equal(-2.0, saved.Right.Dy, 6);
        Assert.Equal(-30.0, saved.Right.RotationDegrees, 6);
        Assert.True(saved.Right.IsMirrored);
        Assert.Equal(-30.0, saved.Right.XMin);
        Assert.Equal(40.0, saved.Right.XMax);
    }

    [Fact]
    public void AutoAlignLeft_WithShiftedCachedProfile_ComposesSingleIcpDelta()
    {
        string settingsPath = BuildTempFilePath();
        string csvPath = BuildStandardProfileCsv(5.0, 2.0);
        var transformService = new ProfileRegistrationTransformService();
        var settingsStore = new ProfileRegistrationSettingsStore(settingsPath);
        var profileService = new PointCloudRepresentativeProfileService(settingsStore, transformService);
        var viewModel = new ProfileRegistrationViewModel(settingsStore, transformService);
        List<PointCloudPoint3D> rawPoints = PointCloudCsvReader.ReadPointsFromCsv(csvPath);
        var initialParameters = new ProfileRegistrationParameters(0.0, 0.0, 0.0);
        IReadOnlyList<RailProfilePoint> currentProfile = profileService
            .ExtractMedianSectionProfileFromPoints(
                rawPoints,
                PointCloudDeviceSide.Left,
                initialParameters)
            .ProfilePoints;
        ProfileRegistrationParameters icpDelta = new RobustIcpRegistrationService()
            .Align(currentProfile, BuildStandardPoints());
        ProfileRegistrationParameters expected = transformService.ComposeRawFirstWithGlobalDelta(
            rawPoints,
            initialParameters,
            icpDelta,
            PointCloudDeviceSide.Left);
        viewModel.PendingRightDx = 17.0;
        viewModel.PendingRightDy = -9.0;
        viewModel.ApplyPendingParameters();
        viewModel.SetLeftFilePath(csvPath);
        viewModel.PendingLeftIsMirrored = true;
        viewModel.PendingLeftXMin = -50.0;
        viewModel.PendingLeftXMax = 50.0;

        viewModel.AutoAlignLeft();

        Assert.Equal(expected.Dx, viewModel.AppliedLeftDx, 6);
        Assert.Equal(expected.Dy, viewModel.AppliedLeftDy, 6);
        Assert.Equal(expected.RotationDegrees, viewModel.AppliedLeftRotationDegrees, 6);
        Assert.Equal(viewModel.AppliedLeftDx, viewModel.PendingLeftDx, 6);
        Assert.Equal(viewModel.AppliedLeftDy, viewModel.PendingLeftDy, 6);
        Assert.Equal(viewModel.AppliedLeftRotationDegrees, viewModel.PendingLeftRotationDegrees, 6);
        Assert.False(viewModel.PendingLeftIsMirrored);
        Assert.Null(viewModel.PendingLeftXMin);
        Assert.Null(viewModel.PendingLeftXMax);
        Assert.Equal(17.0, viewModel.AppliedRightDx, 6);
        Assert.Equal(-9.0, viewModel.AppliedRightDy, 6);
    }

    [Fact]
    public void AutoAlignLeft_WithNegligibleIcpDelta_DoesNotApplyPendingParameters()
    {
        string csvPath = BuildStandardProfileCsv(0.0, 0.0);
        ProfileRegistrationViewModel viewModel = CreateViewModel();
        viewModel.SetLeftFilePath(csvPath);
        viewModel.PendingLeftDx = 123.0;
        viewModel.PendingLeftDy = -45.0;
        viewModel.PendingLeftRotationDegrees = 67.0;

        viewModel.AutoAlignLeft();

        Assert.Equal(123.0, viewModel.PendingLeftDx, 6);
        Assert.Equal(-45.0, viewModel.PendingLeftDy, 6);
        Assert.Equal(67.0, viewModel.PendingLeftRotationDegrees, 6);
        Assert.Equal(0.0, viewModel.AppliedLeftDx, 6);
        Assert.Equal(0.0, viewModel.AppliedLeftDy, 6);
        Assert.Equal(0.0, viewModel.AppliedLeftRotationDegrees, 6);
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

    private static string BuildStandardProfileCsv(double xOffset, double zOffset)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            "grindcar-tests",
            Guid.NewGuid().ToString("N"),
            "profile.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var builder = new StringBuilder("X,Y,Z\r\n");
        for (int sectionIndex = 0; sectionIndex < 5; sectionIndex++)
        {
            double y = sectionIndex + 1.0;
            for (int pointIndex = 0; pointIndex <= 240; pointIndex++)
            {
                double standardX = StandardRailProfileSolver.LeftBoundaryX +
                    (StandardRailProfileSolver.RightBoundaryX - StandardRailProfileSolver.LeftBoundaryX) *
                    pointIndex / 240.0;
                double x = standardX + xOffset;
                double z = StandardRailProfileSolver.RailSurfaceFun(standardX) + zOffset;
                builder.Append(x.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(y.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(z.ToString("R", CultureInfo.InvariantCulture));
                builder.Append("\r\n");
            }
        }

        File.WriteAllText(filePath, builder.ToString());
        return filePath;
    }

    private static IReadOnlyList<RailProfilePoint> BuildStandardPoints()
    {
        var points = new List<RailProfilePoint>(241);
        for (int index = 0; index <= 240; index++)
        {
            double x = StandardRailProfileSolver.LeftBoundaryX +
                (StandardRailProfileSolver.RightBoundaryX - StandardRailProfileSolver.LeftBoundaryX) *
                index / 240.0;
            double y = StandardRailProfileSolver.RailSurfaceFun(x);
            if (!double.IsNaN(y) && !double.IsInfinity(y))
            {
                points.Add(new RailProfilePoint(x, y));
            }
        }

        return points;
    }
}
