using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GrindCar.Services.Curve;
using Microsoft.Win32;

namespace GrindCar.Views;

/// <summary>
/// 曲线旋转调试窗口：导入二维点并按指定角度进行逆时针旋转可视化。
/// </summary>
public partial class CurveRotationDebugWindow : Window
{
    private const string CsvFilter = "CSV 文件|*.csv";
    private const double PlotPadding = 36.0;

    private readonly List<Point> _originalPoints = new();
    private List<Point> _rotatedPoints = new();

    /// <summary>
    /// 初始化旋转调试窗口。
    /// </summary>
    public CurveRotationDebugWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 导入 CSV 并初始化原始曲线与旋转曲线。
    /// </summary>
    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 CSV 曲线文件",
            Filter = CsvFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            List<Point> points = CurveCsvReader.ReadPointsFromCsv(dialog.FileName);
            if (points.Count == 0)
            {
                throw new InvalidOperationException("CSV 中未解析到有效点。");
            }

            _originalPoints.Clear();
            _originalPoints.AddRange(points);
            _rotatedPoints = points.ToList();

            FilePathTextBlock.Text = dialog.FileName;
            RotateButton.IsEnabled = true;
            UpdateSummaryTexts();
            RedrawPlot();
            StatusTextBlock.Text = $"导入成功，共 {_originalPoints.Count.ToString(CultureInfo.InvariantCulture)} 个点。";
        }
        catch (Exception ex)
        {
            _originalPoints.Clear();
            _rotatedPoints.Clear();
            RotateButton.IsEnabled = false;
            FilePathTextBlock.Text = "未导入文件";
            ResetSummaryTexts();
            PlotCanvas.Children.Clear();
            StatusTextBlock.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 读取角度并应用逆时针旋转。
    /// </summary>
    private void ApplyRotation_Click(object sender, RoutedEventArgs e)
    {
        if (_originalPoints.Count == 0)
        {
            StatusTextBlock.Text = "请先导入 CSV 数据。";
            return;
        }

        if (!CurveRotationService.TryParseAngle(AngleTextBox.Text, out double angleDegrees))
        {
            StatusTextBlock.Text = "角度输入无效，请输入数值。";
            MessageBox.Show(this, "角度输入无效，请输入数值。", "旋转失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _rotatedPoints = CurveRotationService.RotateCounterclockwise(_originalPoints, angleDegrees);
        UpdateSummaryTexts();
        RedrawPlot();
        StatusTextBlock.Text =
            $"已将曲线绕原点逆时针旋转 {angleDegrees.ToString("0.###", CultureInfo.InvariantCulture)}°。";
    }

    /// <summary>
    /// 画布尺寸变化时重绘图像。
    /// </summary>
    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_originalPoints.Count == 0)
        {
            return;
        }

        RedrawPlot();
    }

    /// <summary>
    /// 关闭当前窗口。
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 重新绘制坐标轴与原始/旋转曲线。
    /// </summary>
    private void RedrawPlot()
    {
        PlotCanvas.Children.Clear();
        double width = PlotCanvas.ActualWidth;
        double height = PlotCanvas.ActualHeight;
        if (width <= 2 * PlotPadding || height <= 2 * PlotPadding)
        {
            return;
        }

        IReadOnlyList<Point> displayedPoints = _rotatedPoints.Count > 0 ? _rotatedPoints : _originalPoints;
        if (displayedPoints.Count == 0)
        {
            return;
        }

        double minX = displayedPoints.Min(point => point.X);
        double maxX = displayedPoints.Max(point => point.X);
        double minY = displayedPoints.Min(point => point.Y);
        double maxY = displayedPoints.Max(point => point.Y);

        if (Math.Abs(maxX - minX) < double.Epsilon)
        {
            minX -= 1.0;
            maxX += 1.0;
        }

        if (Math.Abs(maxY - minY) < double.Epsilon)
        {
            minY -= 1.0;
            maxY += 1.0;
        }

        double xScale = (width - 2.0 * PlotPadding) / (maxX - minX);
        double yScale = (height - 2.0 * PlotPadding) / (maxY - minY);

        Point ToScreen(Point worldPoint)
        {
            double x = PlotPadding + (worldPoint.X - minX) * xScale;
            double y = height - PlotPadding - (worldPoint.Y - minY) * yScale;
            return new Point(x, y);
        }

        DrawAxes(minX, maxX, minY, maxY, ToScreen);
        DrawPoints(displayedPoints, ToScreen, "#1F77B4");
    }

    /// <summary>
    /// 绘制 X/Y 轴。
    /// </summary>
    private void DrawAxes(double minX, double maxX, double minY, double maxY, Func<Point, Point> toScreen)
    {
        double axisY = minY <= 0.0 && maxY >= 0.0 ? 0.0 : minY;
        double axisX = minX <= 0.0 && maxX >= 0.0 ? 0.0 : minX;

        Point xAxisStart = toScreen(new Point(minX, axisY));
        Point xAxisEnd = toScreen(new Point(maxX, axisY));
        Point yAxisStart = toScreen(new Point(axisX, minY));
        Point yAxisEnd = toScreen(new Point(axisX, maxY));

        PlotCanvas.Children.Add(new Line
        {
            X1 = xAxisStart.X,
            Y1 = xAxisStart.Y,
            X2 = xAxisEnd.X,
            Y2 = xAxisEnd.Y,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AEBBC4")),
            StrokeThickness = 1.1
        });

        PlotCanvas.Children.Add(new Line
        {
            X1 = yAxisStart.X,
            Y1 = yAxisStart.Y,
            X2 = yAxisEnd.X,
            Y2 = yAxisEnd.Y,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AEBBC4")),
            StrokeThickness = 1.1
        });
    }

    /// <summary>
    /// 绘制指定点集的散点。
    /// </summary>
    private void DrawPoints(IReadOnlyList<Point> points, Func<Point, Point> toScreen, string hexColor)
    {
        if (points.Count == 0)
        {
            return;
        }

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        const double markerSize = 3.2;
        const double halfMarkerSize = markerSize / 2.0;

        for (int index = 0; index < points.Count; index++)
        {
            Point point = toScreen(points[index]);
            var marker = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = brush
            };

            Canvas.SetLeft(marker, point.X - halfMarkerSize);
            Canvas.SetTop(marker, point.Y - halfMarkerSize);
            PlotCanvas.Children.Add(marker);
        }
    }

    /// <summary>
    /// 更新统计信息文本。
    /// </summary>
    private void UpdateSummaryTexts()
    {
        PointCountTextBlock.Text = _originalPoints.Count.ToString(CultureInfo.InvariantCulture);
        if (_originalPoints.Count == 0)
        {
            ResetSummaryTexts();
            return;
        }

        double minX = _originalPoints.Min(point => point.X);
        double maxX = _originalPoints.Max(point => point.X);
        double minY = _originalPoints.Min(point => point.Y);
        double maxY = _originalPoints.Max(point => point.Y);
        XRangeTextBlock.Text = $"{minX:0.###} ~ {maxX:0.###}";
        YRangeTextBlock.Text = $"{minY:0.###} ~ {maxY:0.###}";

        if (_rotatedPoints.Count > 0)
        {
            double rotatedMinY = _rotatedPoints.Min(point => point.Y);
            double rotatedMaxY = _rotatedPoints.Max(point => point.Y);
            RotatedYRangeTextBlock.Text = $"{rotatedMinY:0.###} ~ {rotatedMaxY:0.###}";
        }
        else
        {
            RotatedYRangeTextBlock.Text = "-";
        }
    }

    /// <summary>
    /// 重置统计文本。
    /// </summary>
    private void ResetSummaryTexts()
    {
        PointCountTextBlock.Text = "0";
        XRangeTextBlock.Text = "-";
        YRangeTextBlock.Text = "-";
        RotatedYRangeTextBlock.Text = "-";
    }
}