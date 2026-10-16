using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GrindCar.Services.Measurement;
using GrindCar.Views;
using Xunit;

namespace GrindCar.Tests;

public partial class GrindingWindowTests
{
    private static void VerifyRoundedInputs(Application application)
    {
        var input = new TextBox { Text = "测试输入", Width = 220, Height = 34 };
        var compact = new TextBox
        {
            Text = "192.168.1.10 / 502", Width = 220, Height = 26,
            Style = (Style)application.FindResource("CompactTextBoxStyle")
        };
        var multiline = new TextBox
        {
            Text = string.Join(Environment.NewLine, Enumerable.Range(1, 20)),
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var combo = new ComboBox { Width = 220, MaxDropDownHeight = 100, ItemsSource = Enumerable.Range(1, 20), SelectedIndex = 0 };
        var panel = new StackPanel();
        panel.Children.Add(input);
        panel.Children.Add(compact);
        panel.Children.Add(multiline);
        panel.Children.Add(combo);
        var host = new Window { Content = panel, Width = 320, Height = 300, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            host.Show();
            foreach (string theme in new[] { "Dark", "Light" })
            {
                application.Resources.MergedDictionaries[0] = new ResourceDictionary
                {
                    Source = new Uri($"/GrindCar;component/Themes/{theme}Theme.xaml", UriKind.Relative)
                };
                PumpInputLayout(host);
                var border = (Border)input.Template.FindName("InputBorder", input);
                Assert.Equal(new CornerRadius(6), border.CornerRadius);
                Assert.IsType<ScrollViewer>(input.Template.FindName("PART_ContentHost", input));
                input.SelectAll();
                Assert.Equal(input.Text, input.SelectedText);
                input.IsReadOnly = true;
                Assert.True(ApplicationCommands.Copy.CanExecute(null, input));
                EditingCommands.Backspace.Execute(null, input);
                Assert.Equal("测试输入", input.Text);
                input.IsReadOnly = false;
                input.IsEnabled = false;
                Assert.Equal(0.45, border.Opacity);
                input.IsEnabled = true;
                multiline.ScrollToEnd();
                PumpInputLayout(host);
                Assert.True(multiline.VerticalOffset > 0);

                var comboBorder = (Border)combo.Template.FindName("InputBorder", combo);
                Assert.Equal(new CornerRadius(6), comboBorder.CornerRadius);
                combo.IsDropDownOpen = true;
                PumpInputLayout(host);
                var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                Assert.True(popup.IsOpen);
                Assert.True(popup.Child.RenderSize.Height <= combo.MaxDropDownHeight);
                Assert.NotNull(combo.ItemContainerGenerator.ContainerFromIndex(0));
                combo.IsDropDownOpen = false;
                PumpInputLayout(host);
                combo.Focus();
                combo.SelectedIndex = 0;
                combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(combo), 0, Key.Down)
                {
                    RoutedEvent = Keyboard.KeyDownEvent
                });
                Assert.Equal(1, combo.SelectedIndex);
                combo.IsDropDownOpen = true;
                PumpInputLayout(host);
                combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(combo), 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.KeyDownEvent
                });
                Assert.False(combo.IsDropDownOpen);
                foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
                {
                    foreach (TextBox textBox in new[] { input, compact })
                    {
                        textBox.LayoutTransform = new ScaleTransform(scale, scale);
                        PumpInputLayout(host);
                        AssertTextFitsViewport(textBox);
                        textBox.Focus();
                        PumpInputLayout(host);
                        AssertTextFitsViewport(textBox);
                        textBox.LayoutTransform = Transform.Identity;
                    }
                }
            }
        }
        finally { host.Close(); }

        (string Text, bool HasError)? baselineCancelState = null;
        foreach (bool rounded in new[] { false, true })
        {
        var confirmation = new GrindDepthConfirmationWindow(new[] { new MeasurementGrindingTimesResult(0, 0.1, 2) })
        {
            Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false
        };
        try
        {
            confirmation.Show();
            PumpInputLayout(confirmation);
            DataGrid grid = Descendants(confirmation).OfType<DataGrid>().Single();
            // 对照系统模板，确保只改变外观，不改变既有即时更新绑定的取消语义。
            if (!rounded) ((DataGridTextColumn)grid.Columns[2]).EditingElementStyle = new Style(typeof(TextBox));
            grid.SelectedItem = confirmation.Items[0];
            grid.CurrentCell = new DataGridCellInfo(confirmation.Items[0], grid.Columns[2]);
            Assert.True(grid.BeginEdit());
            PumpInputLayout(confirmation);
            TextBox editor = Descendants(grid).OfType<TextBox>().Single();
            if (rounded)
                Assert.Equal(new CornerRadius(6), ((Border)editor.Template.FindName("InputBorder", editor)).CornerRadius);
            editor.Text = "0.25";
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
            Assert.Equal(0.25, confirmation.Items[0].ConfirmedDepth);
            Assert.True(grid.BeginEdit());
            PumpInputLayout(confirmation);
            editor = Descendants(grid).OfType<TextBox>().Single();
            editor.Text = "invalid";
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.True(confirmation.Items[0].HasError);
            Assert.False(confirmation.Items[0].ValidateConfirmedDepth());
            Assert.True(grid.CancelEdit(DataGridEditingUnit.Cell));
            PumpInputLayout(confirmation);
            Assert.Equal(0.25, confirmation.Items[0].ConfirmedDepth);
            var cancelState = (confirmation.Items[0].ConfirmedDepthText, confirmation.Items[0].HasError);
            if (!rounded) baselineCancelState = cancelState;
            else Assert.Equal(baselineCancelState!.Value, cancelState);
        }
        finally { confirmation.Close(); }
        }
    }

    private static void PumpInputLayout(Window window)
    {
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void AssertTextFitsViewport(TextBox textBox)
    {
        var viewport = Descendants(textBox).OfType<ScrollContentPresenter>().First();
        Rect bounds = viewport.TransformToAncestor(textBox).TransformBounds(new Rect(viewport.RenderSize));
        Rect character = textBox.GetRectFromCharacterIndex(0);
        Assert.False(character.IsEmpty);
        Assert.True(character.Top >= bounds.Top - 1, $"文字顶部 {character.Top} 超出视口 {bounds}");
        Assert.True(character.Bottom <= bounds.Bottom + 1, $"文字底部 {character.Bottom} 超出视口 {bounds}");
    }
}
