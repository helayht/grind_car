using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GrindCar.Services;
using GrindCar.Services.Grinding;
using GrindCar.Services.Measurement;
using GrindCar.Models.Grinding;
using GrindCar.Models.Rail;
using GrindCar.ViewModels;
using GrindCar.Views;
using Xunit;

namespace GrindCar.Tests;

public partial class GrindingWindowTests
{
    [Fact]
    public void Window_ThemesAndSmallScreen_RendersAndStopsPolling()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                using var connection = new SharedPlcConnectionService("127.0.0.1", 502, 1);
                var measurement = new MainWindowMeasurementViewModel(new MeasurementParameterService(), connection, "127.0.0.1", 502);
                var service = new StateService();
                using var vm = new GrindingParametersViewModel(service,
                    new GrindingSettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), connection, measurement);
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/GrindCar;component/Themes/SharedStyles.xaml", UriKind.Relative)
                });
                var window = new GrindingParametersView(vm);
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"/GrindCar;component/Themes/{theme}Theme.xaml", UriKind.Relative)
                    });
                    foreach (Size size in new[] { new Size(1040, 720), new Size(680, 440) })
                    {
                        var content = (FrameworkElement)window.Content;
                        content.Measure(size);
                        content.Arrange(new Rect(size));
                        content.UpdateLayout();
                        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        Assert.Equal(size.Width, content.ActualWidth);
                        var buttons = Descendants(content).OfType<Button>().Where(button => button.Content is string).ToArray();
                        Assert.Equal(3, buttons.Length);
                        Assert.True(buttons.Single(button => (string)button.Content == "保存本地").IsEnabled);
                        Assert.False(buttons.Single(button => (string)button.Content == "写入全部参数").IsEnabled);
                        foreach (Button button in buttons)
                        {
                            Point origin = button.TranslatePoint(new Point(0, 0), content);
                            Assert.InRange(origin.Y + button.ActualHeight, 0, size.Height);
                        }
                        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(content);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        string directory = Path.Combine(AppContext.BaseDirectory, "ui-review");
                        Directory.CreateDirectory(directory);
                        using FileStream file = File.Create(Path.Combine(directory, $"grinding-{theme}-{size.Width}.png"));
                        encoder.Save(file);
                    }
                }
                Await(vm.StopPollingAsync());
                RenderDashboard();
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)));
        if (failure != null) throw new InvalidOperationException("窗口渲染验证失败", failure);
    }

    private static void RenderDashboard()
    {
        var application = Application.Current;
        application.Resources.MergedDictionaries.Clear();
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/GrindCar;component/Themes/DarkTheme.xaml", UriKind.Relative)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/GrindCar;component/Themes/SharedStyles.xaml", UriKind.Relative)
        });
        var dashboard = new MainWindow();
        long profileRun = dashboard.Measurement.Profile.Begin(RailProfileType.Kg60);
        dashboard.Measurement.Profile.CreateProgress(profileRun).Report(new MeasurementRepresentativeProfile(1,
            Enumerable.Range(-30, 61).Select(x => new RailProfilePoint(x, RailSurfaceService.RailSurfaceFun(x) + 0.6)).ToArray()));
        dashboard.Measurement.Profile.End(profileRun, failed: false);
        foreach (string theme in new[] { "Dark", "Light" })
        {
            application.Resources.MergedDictionaries[0] = new ResourceDictionary
            {
                Source = new Uri($"/GrindCar;component/Themes/{theme}Theme.xaml", UriKind.Relative)
            };
            foreach (Size size in new[] { new Size(1440, 900), new Size(900, 600) })
            {
                var content = (FrameworkElement)dashboard.Content;
                content.Measure(size);
                content.Arrange(new Rect(size));
                content.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                ScrollViewer scroll = Descendants((FrameworkElement)dashboard.FindName("HomePage")).OfType<ScrollViewer>().First();
                foreach (bool bottom in new[] { false, true })
                {
                    if (bottom) scroll.ScrollToEnd(); else scroll.ScrollToTop();
                    content.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    string directory = Path.Combine(AppContext.BaseDirectory, "ui-review");
                    Directory.CreateDirectory(directory);
                    using FileStream file = File.Create(Path.Combine(directory, $"dashboard-{theme}-{size.Width}-{(bottom ? "bottom" : "top")}.png"));
                    encoder.Save(file);
                }
                Button writeButton = Descendants(content).OfType<Button>().Single(button => Equals(button.Content, "写入测量参数"));
                Assert.False(writeButton.IsEnabled);
                Assert.Contains(Descendants(content).OfType<TextBlock>(), text => text.Text == "廓形仪测量回避位");
                VerifyDashboardLayout(dashboard, content, size, theme);
            }
        }
        VerifyDashboardOfflineEntry(dashboard);
        VerifyNavigation(dashboard, application);
        VerifyMotorLifecycle();
        VerifyDialogButtons(application);
        VerifyRoundedInputs(application);
        dashboard.Close();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        application.Shutdown();
    }

    private static void Await(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false),
                TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private static void VerifyNavigation(MainWindow dashboard, Application application)
    {
        var host = (ContentControl)dashboard.FindName("PageHost");
        var navigation = (StackPanel)dashboard.FindName("NavigationButtons");
        Assert.Equal(7, navigation.Children.Count);
        int windowCount = application.Windows.Count;
        foreach (MainPage page in new[] { MainPage.Validation, MainPage.Registration, MainPage.Motor, MainPage.Grinding })
        {
            Await(dashboard.NavigateAsync(page));
            Assert.Equal(page, dashboard.CurrentPage);
            var view = Assert.IsAssignableFrom<UserControl>(host.Content);
            object viewModel = view.DataContext;
            if (viewModel is PointCloudGrindDepthDebugViewModel validation)
            {
                validation.AnglesInput = "0, 5, 10";
                validation.SetLeftFilePath("retained-left.csv");
                validation.Results.Add(new CombinedGrindDepthResult(0, 0.1, 0.2, 0.2));
            }
            if (viewModel is ProfileRegistrationViewModel registration) registration.PendingLeftDx = 12;
            if (viewModel is GrindingParametersViewModel grinding) grinding.BasicFields.First().Text = "123";
            Await(dashboard.NavigateAsync(page));
            Assert.Same(view, host.Content);
            foreach (string theme in new[] { "Dark", "Light" })
            {
                application.Resources.MergedDictionaries[0] = new ResourceDictionary
                {
                    Source = new Uri($"/GrindCar;component/Themes/{theme}Theme.xaml", UriKind.Relative)
                };
                foreach (Size size in new[] { new Size(1440, 900), new Size(900, 600) })
                {
                    var content = (FrameworkElement)dashboard.Content;
                    content.Measure(size);
                    content.Arrange(new Rect(size));
                    content.UpdateLayout();
                    view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(Visibility.Visible, host.Visibility);
                    foreach (Button button in navigation.Children.OfType<Button>())
                    {
                        Point origin = button.TranslatePoint(new Point(), content);
                        Assert.InRange(origin.X + button.ActualWidth, 0, 220);
                        Assert.Equal(54, button.ActualHeight);
                        if (button.Tag is string tag)
                            Assert.Same(dashboard.FindResource(tag == page.ToString()
                                ? "SidebarSelectedButtonStyle" : "SidebarButtonStyle"), button.Style);
                    }
                    if (viewModel is MotorViewModel motor)
                    {
                        var modifyButtons = Descendants(view).OfType<Button>().Where(button => Equals(button.Content, "修改")).ToArray();
                        Assert.NotEmpty(modifyButtons);
                        Assert.All(modifyButtons, button => Assert.Same(motor.ModifyCommand, button.Command));
                        Button toggle = Descendants(view).OfType<Button>().First(button =>
                            button.CommandParameter is MotorParameterItemViewModel item && item.IsBoolWrite);
                        var item = (MotorParameterItemViewModel)toggle.CommandParameter;
                        Assert.Same(motor.ModifyCommand, toggle.Command);
                        foreach (bool state in new[] { false, true })
                        {
                            item.BoolValue = state;
                            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                            Assert.Equal(state ? "复位" : "置位", toggle.Content);
                            Assert.Equal(((SolidColorBrush)application.FindResource(state ? "PrimaryActionBrush" : "SecondaryActionBrush")).Color,
                                ((SolidColorBrush)toggle.Background).Color);
                        }
                        toggle.IsEnabled = false;
                        Assert.Equal(0.45, toggle.Opacity);
                        toggle.ClearValue(UIElement.IsEnabledProperty);
                        item.BoolValue = false;
                    }
                    var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    string directory = Path.Combine(AppContext.BaseDirectory, "ui-review");
                    Directory.CreateDirectory(directory);
                    using FileStream file = File.Create(Path.Combine(directory, $"navigation-{page}-{theme}-{size.Width}.png"));
                    encoder.Save(file);
                }
            }
            dashboard.Measurement.SetWorkflowActive(true);
            Await(dashboard.NavigateAsync(MainPage.Home));
            Assert.Equal(page, dashboard.CurrentPage);
            dashboard.Measurement.SetWorkflowActive(false);
            dashboard.Measurement.SetGrindingParametersBusy(true);
            Await(dashboard.NavigateAsync(MainPage.Home));
            Assert.Equal(page, dashboard.CurrentPage);
            dashboard.Measurement.SetGrindingParametersBusy(false);
            Await(dashboard.NavigateAsync(MainPage.Home));
            Assert.Equal(MainPage.Home, dashboard.CurrentPage);
            Await(dashboard.NavigateAsync(page));
            Assert.Same(view, host.Content);
            Assert.Same(viewModel, view.DataContext);
            if (viewModel is PointCloudGrindDepthDebugViewModel retained)
            {
                Assert.Equal("0, 5, 10", retained.AnglesInput);
                Assert.Equal("retained-left.csv", retained.LeftFilePath);
                Assert.Single(retained.Results);
            }
            if (viewModel is ProfileRegistrationViewModel keptRegistration) Assert.Equal(12, keptRegistration.PendingLeftDx);
            if (viewModel is GrindingParametersViewModel keptGrinding) Assert.Equal("123", keptGrinding.BasicFields.First().Text);
            Assert.Equal(windowCount, application.Windows.Count);
            Await(dashboard.NavigateAsync(MainPage.Home));
        }
    }

    private static void VerifyDialogButtons(Application application)
    {
        Window[] windows =
        {
            new PlcConnectionSettingsWindow("127.0.0.1", 502),
            new PointCloudExportWindow(),
            new GrindDepthConfirmationWindow(Array.Empty<MeasurementGrindingTimesResult>())
        };
        foreach (string theme in new[] { "Dark", "Light" })
        {
            application.Resources.MergedDictionaries[0] = new ResourceDictionary
            {
                Source = new Uri($"/GrindCar;component/Themes/{theme}Theme.xaml", UriKind.Relative)
            };
            foreach (Window window in windows)
            {
                var content = (FrameworkElement)window.Content;
                if (content is Panel panel) panel.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush");
                var size = new Size(window.Width - 16, window.Height - 40);
                content.Measure(size);
                content.Arrange(new Rect(size));
                content.UpdateLayout();
                foreach (Button button in Descendants(content).OfType<Button>().Where(button => button.Content is string))
                {
                    Assert.Same(((Style)application.FindResource("PrimaryButtonStyle")).Setters.OfType<Setter>()
                        .Single(setter => setter.Property == Control.TemplateProperty).Value, button.Template);
                    Assert.True(button.ActualHeight >= 34);
                    Point origin = button.TranslatePoint(new Point(), content);
                    Assert.InRange(origin.Y + button.ActualHeight, 0, size.Height);
                }
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream file = File.Create(Path.Combine(AppContext.BaseDirectory, "ui-review", $"buttons-{window.GetType().Name}-{theme}.png"));
                encoder.Save(file);
            }
        }
        foreach (Window window in windows) window.Close();
    }

    private static void VerifyMotorLifecycle()
    {
        using var connection = new SharedPlcConnectionService("127.0.0.1", 502, 1);
        var clientField = typeof(SharedPlcConnectionService).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var notify = typeof(SharedPlcConnectionService).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var client = new ConnectedClient();
        clientField.SetValue(connection, client);
        using var view = new MotorDebugView(connection);
        var vm = (MotorViewModel)view.DataContext;
        Await(view.ActivateAsync());
        Assert.True(vm.IsConnected);
        Assert.True(client.ReadCount > 0);
        // 快速离开再进入也必须等待同一个旧轮询退出。
        Task leave = view.DeactivateAsync();
        Task enter = view.ActivateAsync();
        Await(Task.WhenAll(leave, enter));
        Assert.True(vm.IsConnected);
        Await(view.DeactivateAsync());
        int count = client.ReadCount;
        Await(Task.Delay(1100));
        Assert.Equal(count, client.ReadCount);
        Assert.True(connection.IsConnected);
        Assert.False(client.DisconnectCalled);
        Await(view.ActivateAsync());
        connection.Disconnect();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.False(vm.IsConnected);
        var reconnected = new ConnectedClient();
        clientField.SetValue(connection, reconnected);
        notify.Invoke(connection, new object[] { nameof(SharedPlcConnectionService.IsConnected) });
        Await(Task.Delay(50));
        Assert.True(vm.IsConnected);
        Assert.True(reconnected.ReadCount > 0);
        Await(view.DeactivateAsync());
        view.Dispose();
        Assert.True(connection.IsConnected);
        Assert.False(reconnected.DisconnectCalled);
    }

    [Fact]
    public async Task Polling_StateTransitionsAndMeasurementLock_AreApplied()
    {
        using var connection = new SharedPlcConnectionService("127.0.0.1", 502, 1);
        // 用内存客户端替换私有连接，仅模拟共享连接状态，不连接真实PLC。
        typeof(SharedPlcConnectionService).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(connection, new ConnectedClient());
        var measurement = new MainWindowMeasurementViewModel(new MeasurementParameterService(), connection, "127.0.0.1", 502);
        var service = new StateService();
        using var vm = new GrindingParametersViewModel(service,
            new GrindingSettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), connection, measurement);
        vm.StartPolling();
        Assert.True(vm.CanWrite);
        vm.SetMeasurementActive(true);
        Assert.False(vm.CanWrite);
        vm.SetMeasurementActive(false);
        Assert.True(vm.CanWrite);
        foreach (GrindingFieldViewModel field in vm.BasicFields.Concat(vm.Angles.SelectMany(angle => angle.Fields))) field.Text = "1";
        service.PendingWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task write = vm.WriteAsync(false);
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanWrite);
        Assert.False(measurement.CanOperate);
        service.PendingWrite.SetResult(true);
        await write;
        Assert.False(vm.IsBusy);
        Assert.True(measurement.CanOperate);
        Assert.Contains("成功", vm.WriteStatus);
        await vm.StopPollingAsync();
        Assert.False(vm.CanWrite);
        service.Running = true;
        vm.StartPolling();
        Assert.False(vm.CanWrite);
        Assert.Equal("打磨运行中", vm.RunningStatus);
        await vm.StopPollingAsync();
        service.Fail = true;
        vm.StartPolling();
        Assert.Contains("未知", vm.RunningStatus);
        Assert.False(vm.CanWrite);
        await vm.StopPollingAsync();
        int count = service.ReadCount;
        await Task.Delay(600);
        Assert.Equal(count, service.ReadCount);
        service.Fail = false;
        service.Running = false;
        vm.StartPolling();
        Assert.True(vm.CanWrite);
        connection.Disconnect();
        Assert.False(vm.CanWrite);
        Assert.Contains("未知", vm.RunningStatus);
        await vm.StopPollingAsync();
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class StateService : IGrindingParameterService
    {
        public bool Running { get; set; }
        public bool Fail { get; set; }
        public int ReadCount { get; private set; }
        public TaskCompletionSource<bool>? PendingWrite { get; set; }
        public Task<bool> ReadRunningAsync(CancellationToken token)
        {
            ReadCount++;
            return Fail ? Task.FromException<bool>(new IOException("模拟断线")) : Task.FromResult(Running);
        }
        public Task WriteAllAsync(GrindingSettings settings, CancellationToken token) => PendingWrite?.Task ?? Task.CompletedTask;
        public Task WriteAngleAsync(GrindingAngleSettings settings, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class ConnectedClient : IPlcClient
    {
        public bool IsConnected => true;
        public bool DisconnectCalled { get; private set; }
        public int ReadCount { get; private set; }
        public Task ConnectAsync() => Task.CompletedTask;
        public void Disconnect() { DisconnectCalled = true; }
        public void Dispose() { }
        public int ReadInt32(ushort address) { ReadCount++; return 0; }
        public bool ReadSingleCoil(ushort address) => false;
        public void WriteInt32(ushort address, int value) { }
        public void WriteInt16(ushort address, short value) { }
        public Task WriteSingleCoilAsync(ushort address, bool value) => Task.CompletedTask;
    }
}

