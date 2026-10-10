using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GrindCar.Views;

public enum MainPage
{
    Home,
    Validation,
    Registration,
    Motor,
    Grinding
}

public partial class MainWindow
{
    private MotorDebugView? _motorView;
    private GrindingParametersView? _grindingView;
    private PointCloudGrindDepthDebugView? _validationView;
    private ProfileRegistrationView? _registrationView;
    private bool _navigating;

    public MainPage CurrentPage { get; private set; } = MainPage.Home;

    private async void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && Enum.TryParse(button.Tag as string, out MainPage page))
            await NavigateAsync(page);
    }

    private async void ReturnHomeRequested(object? sender, EventArgs e) => await NavigateAsync(MainPage.Home);

    /// <summary>所有页面切换共用忙碌保护；缓存页面但不保留隐藏页面的轮询。</summary>
    public async Task NavigateAsync(MainPage page)
    {
        if (_navigating || _shutdownPending || !Measurement.CanOperate || _grindingViewModel.IsBusy || page == CurrentPage)
            return;

        _navigating = true;
        NavigationButtons.IsEnabled = false;
        PageArea.IsEnabled = false;
        try
        {
            UserControl? next = GetPage(page);
            if (CurrentPage == MainPage.Motor && _motorView != null)
                await _motorView.DeactivateAsync();
            if (CurrentPage == MainPage.Grinding)
                await _grindingViewModel.StopPollingAsync();

            PageHost.Content = next;
            HomePage.Visibility = page == MainPage.Home ? Visibility.Visible : Visibility.Collapsed;
            PageHost.Visibility = page == MainPage.Home ? Visibility.Collapsed : Visibility.Visible;
            CurrentPage = page;
            foreach (UIElement child in NavigationButtons.Children)
            {
                if (child is Button button && button.Tag is string tag)
                    button.SetResourceReference(StyleProperty,
                        tag == page.ToString() ? "SidebarSelectedButtonStyle" : "SidebarButtonStyle");
            }

            if (page == MainPage.Motor) await _motorView!.ActivateAsync();
            if (page == MainPage.Grinding) _grindingViewModel.StartPolling();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"页面切换失败：{ex.Message}", "页面导航", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _navigating = false;
            NavigationButtons.IsEnabled = true;
            PageArea.IsEnabled = true;
        }
    }

    private UserControl? GetPage(MainPage page)
    {
        switch (page)
        {
            case MainPage.Home:
                return null;
            case MainPage.Motor:
                return _motorView ??= new MotorDebugView(_plcConnection);
            case MainPage.Grinding:
                return _grindingView ??= new GrindingParametersView(_grindingViewModel);
            case MainPage.Validation:
                if (_validationView == null)
                {
                    _validationView = new PointCloudGrindDepthDebugView((owner, calculate) => RunResultWorkflowAsync(owner, calculate, offline: true));
                    _validationView.ReturnHomeRequested += ReturnHomeRequested;
                }
                return _validationView;
            case MainPage.Registration:
                if (_registrationView == null)
                {
                    _registrationView = new ProfileRegistrationView();
                    _registrationView.ReturnHomeRequested += ReturnHomeRequested;
                }
                return _registrationView;
            default:
                throw new ArgumentOutOfRangeException(nameof(page));
        }
    }
}
