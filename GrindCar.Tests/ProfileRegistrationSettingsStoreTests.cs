using System;
using System.IO;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Core;
using Xunit;

namespace GrindCar.Tests;

public class ProfileRegistrationSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_WithValidSettings_RoundTrips()
    {
        string filePath = BuildTempFilePath();
        var store = new ProfileRegistrationSettingsStore(filePath);
        var settings = new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(1.0, 2.0, 3.0, true),
            Right = new ProfileRegistrationParameters(-1.0, -2.0, -3.0)
        };

        store.Save(settings);
        ProfileRegistrationSettings? loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(1.0, loaded.Left!.Dx, 6);
        Assert.True(loaded.Left.IsMirrored);
        Assert.Equal(-2.0, loaded.Right!.Dy, 6);
        Assert.Equal(-3.0, loaded.Right.RotationDegrees, 6);
        Assert.False(loaded.Right.IsMirrored);
    }

    [Fact]
    public void Load_WithLegacyJsonWithoutMirror_LoadsMirrorAsFalse()
    {
        string filePath = BuildTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            "{\n" +
            "  \"Left\": {\n" +
            "    \"Dx\": 1,\n" +
            "    \"Dy\": 2,\n" +
            "    \"RotationDegrees\": 3\n" +
            "  },\n" +
            "  \"Right\": {\n" +
            "    \"Dx\": -1,\n" +
            "    \"Dy\": -2,\n" +
            "    \"RotationDegrees\": -3\n" +
            "  }\n" +
            "}");
        var store = new ProfileRegistrationSettingsStore(filePath);

        ProfileRegistrationSettings loaded = store.LoadRequired();

        Assert.False(loaded.Left!.IsMirrored);
        Assert.False(loaded.Right!.IsMirrored);
    }

    [Fact]
    public void LoadRequired_WithoutFile_ThrowsClearMessage()
    {
        var store = new ProfileRegistrationSettingsStore(BuildTempFilePath());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.LoadRequired());

        Assert.Contains("请先完成代表廓形手动配准并保存参数", exception.Message);
    }

    [Fact]
    public void Load_WithMissingRight_ThrowsClearMessage()
    {
        string filePath = BuildTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            "{\n  \"Left\": {\n    \"Dx\": 1,\n    \"Dy\": 2,\n    \"RotationDegrees\": 3\n  }\n}");
        var store = new ProfileRegistrationSettingsStore(filePath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains("缺少 Right", exception.Message);
    }

    [Fact]
    public void Load_WithInvalidJson_ThrowsClearMessage()
    {
        string filePath = BuildTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "{");
        var store = new ProfileRegistrationSettingsStore(filePath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains("格式无效", exception.Message);
    }

    [Fact]
    public void Save_WithInvalidNumber_ThrowsClearMessage()
    {
        var store = new ProfileRegistrationSettingsStore(BuildTempFilePath());
        var settings = new ProfileRegistrationSettings
        {
            Left = new ProfileRegistrationParameters(double.NaN, 0.0, 0.0),
            Right = new ProfileRegistrationParameters(0.0, 0.0, 0.0)
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Save(settings));

        Assert.Contains("Left 配准参数 Dx", exception.Message);
    }

    private static string BuildTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), "grindcar-tests", Guid.NewGuid().ToString("N"), "point-cloud-profile-registration.json");
    }
}
