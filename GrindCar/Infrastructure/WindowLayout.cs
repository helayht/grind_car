using System;
using System.Windows;

namespace GrindCar.Infrastructure;

/// <summary>按 Windows 工作区的逻辑尺寸限制初始窗口，避免高 DPI 下操作区超出屏幕。</summary>
internal static class WindowLayout
{
    private const double WorkAreaMargin = 24;

    public static void FitToWorkArea(Window window)
    {
        Rect workArea = SystemParameters.WorkArea;
        double availableWidth = Math.Max(1, workArea.Width - WorkAreaMargin);   
        double availableHeight = Math.Max(1, workArea.Height - WorkAreaMargin);
        window.MinWidth = Math.Min(window.MinWidth, availableWidth);
        window.MinHeight = Math.Min(window.MinHeight, availableHeight);
        window.Width = Math.Min(window.Width, availableWidth);
        window.Height = Math.Min(window.Height, availableHeight);
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
    }
}
