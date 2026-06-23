using System.Windows;
using GrindCar.Services;

namespace GrindCar
{
    /// <summary>
    /// 应用入口逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeService.LoadAndApplyTheme();
        }
    }
}
