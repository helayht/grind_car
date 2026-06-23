using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace GrindCar.Services;

/// <summary>
/// 主题类型枚举。
/// </summary>
public enum ThemeType
{
    Dark,
    Light
}

/// <summary>
/// 主题服务：负责主题的持久化、加载与切换。
/// 通过替换 Application.Resources.MergedDictionaries 实现运行时主题切换。
/// </summary>
public static class ThemeService
{
    private const string SettingsFileName = "theme_settings.json";
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";
    private const string SharedStylesUri = "Themes/SharedStyles.xaml";

    private static readonly string SettingsFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        SettingsFileName);

    private static ThemeType _currentTheme = ThemeType.Dark;

    /// <summary>
    /// 当前主题。
    /// </summary>
    public static ThemeType CurrentTheme => _currentTheme;

    /// <summary>
    /// 持久化主题设置。
    /// </summary>
    private class ThemeSettings
    {
        public string Theme { get; set; } = "Dark";
    }

    /// <summary>
    /// 从持久化设置中加载主题，并应用到应用级资源中。
    /// 应在 App 启动时调用一次。
    /// </summary>
    public static void LoadAndApplyTheme()
    {
        ThemeType savedTheme = LoadSavedTheme();
        ApplyTheme(savedTheme);
    }

    /// <summary>
    /// 切换主题（Dark ↔ Light），持久化并立即应用。
    /// </summary>
    public static void ToggleTheme()
    {
        ThemeType newTheme = _currentTheme == ThemeType.Dark
            ? ThemeType.Light
            : ThemeType.Dark;
        ApplyTheme(newTheme);
        SaveTheme(newTheme);
    }

    /// <summary>
    /// 从文件读取上次保存的主题。
    /// </summary>
    private static ThemeType LoadSavedTheme()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                ThemeSettings? settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                if (settings?.Theme == "Light")
                {
                    return ThemeType.Light;
                }
            }
        }
        catch
        {
            // 读取失败时回退到默认暗色主题
        }

        return ThemeType.Dark;
    }

    /// <summary>
    /// 将主题选择写入文件。
    /// </summary>
    private static void SaveTheme(ThemeType theme)
    {
        try
        {
            var settings = new ThemeSettings
            {
                Theme = theme == ThemeType.Light ? "Light" : "Dark"
            };
            string json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // 保存失败时静默忽略
        }
    }

    /// <summary>
    /// 应用指定主题：替换 Application 的 MergedDictionaries。
    /// </summary>
    private static void ApplyTheme(ThemeType theme)
    {
        _currentTheme = theme;

        var appResources = Application.Current.Resources;

        // 清空已有主题字典（保留非主题字典）
        for (int i = appResources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            string source = appResources.MergedDictionaries[i].Source?.OriginalString ?? "";
            if (source.Contains("Themes/"))
            {
                appResources.MergedDictionaries.RemoveAt(i);
            }
        }

        // 按顺序添加：先主题画笔，再共享样式
        string themeUri = theme == ThemeType.Dark ? DarkThemeUri : LightThemeUri;

        appResources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.Relative)
        });

        appResources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(SharedStylesUri, UriKind.Relative)
        });

        // 更新当前所有打开窗口的 Background
        foreach (Window window in Application.Current.Windows)
        {
            // DynamicResource 会自动更新引用，但 Window.Background 是直接设置的属性
            // 如果窗口 Background 使用了 DynamicResource 绑定则无需手动处理
        }
    }
}
