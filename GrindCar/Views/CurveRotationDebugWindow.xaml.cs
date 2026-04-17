using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
    private string _currentCsvPath = "未导入文件";

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
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
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
            List<Point> points = ReadPointsFromCsv(dialog.FileName);
            if (points.Count == 0)
            {
                throw new InvalidOperationException("CSV 中未解析到有效点。");
            }

            _originalPoints.Clear();
            _originalPoints.AddRange(points);
            _rotatedPoints = points.ToList();
            _currentCsvPath = dialog.FileName;

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
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
    private void ApplyRotation_Click(object sender, RoutedEventArgs e)
    {
        if (_originalPoints.Count == 0)
        {
            StatusTextBlock.Text = "请先导入 CSV 数据。";
            return;
        }

        if (!TryParseAngle(AngleTextBox.Text, out double angleDegrees))
        {
            StatusTextBlock.Text = "角度输入无效，请输入数值。";
            MessageBox.Show(this, "角度输入无效，请输入数值。", "旋转失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _rotatedPoints = RotateCounterclockwise(_originalPoints, angleDegrees);
        UpdateSummaryTexts();
        RedrawPlot();
        StatusTextBlock.Text =
            $"已将曲线绕原点逆时针旋转 {angleDegrees.ToString("0.###", CultureInfo.InvariantCulture)}°。";
    }

    /// <summary>
    /// 画布尺寸变化时重绘图像。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">尺寸变化事件参数。</param>
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
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">按钮点击事件参数。</param>
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
    /// <param name="minX">数据最小 X。</param>
    /// <param name="maxX">数据最大 X。</param>
    /// <param name="minY">数据最小 Y。</param>
    /// <param name="maxY">数据最大 Y。</param>
    /// <param name="toScreen">世界坐标到屏幕坐标变换函数。</param>
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
    /// <param name="points">待绘制点集。</param>
    /// <param name="toScreen">世界坐标到屏幕坐标变换函数。</param>
    /// <param name="hexColor">点颜色。</param>
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
    /// 将点集按角度围绕原点做逆时针旋转。
    /// </summary>
    /// <param name="points">原始点集。</param>
    /// <param name="angleDegrees">旋转角度（度）。</param>
    /// <returns>旋转后的点集。</returns>
    private static List<Point> RotateCounterclockwise(IReadOnlyList<Point> points, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;
        double cosValue = Math.Cos(radians);
        double sinValue = Math.Sin(radians);
        var rotated = new List<Point>(points.Count);

        for (int index = 0; index < points.Count; index++)
        {
            Point point = points[index];
            double rotatedX = point.X * cosValue - point.Y * sinValue;
            double rotatedY = point.X * sinValue + point.Y * cosValue;
            rotated.Add(new Point(rotatedX, rotatedY));
        }

        return rotated;
    }

    /// <summary>
    /// 解析角度文本。
    /// </summary>
    /// <param name="input">输入文本。</param>
    /// <param name="angleDegrees">解析出的角度值。</param>
    /// <returns>解析成功返回 true。</returns>
    private static bool TryParseAngle(string input, out double angleDegrees)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out angleDegrees))
        {
            return true;
        }

        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out angleDegrees);
    }

    /// <summary>
    /// 读取 CSV 中的二维点，支持首行表头。
    /// </summary>
    /// <param name="filePath">CSV 文件路径。</param>
    /// <returns>二维点列表。</returns>
    private static List<Point> ReadPointsFromCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("CSV 路径不能为空。");
        }

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"CSV 文件不存在: {filePath}");
        }

        using var reader = new StreamReader(filePath);
        string? firstLine = ReadFirstNonEmptyLine(reader);
        if (firstLine == null)
        {
            throw new InvalidOperationException("CSV 文件为空。");
        }

        char delimiter = DetectDelimiter(firstLine);
        string[] firstValues = SplitLine(firstLine, delimiter);
        bool hasHeader = firstValues.Any(value => value.Any(char.IsLetter));
        (int xIndex, int yIndex) = hasHeader ? ResolveCoordinateIndexes(firstValues) : (0, 1);

        var points = new List<Point>();
        if (!hasHeader && TryReadPoint(firstValues, xIndex, yIndex, out Point firstPoint))
        {
            points.Add(firstPoint);
        }

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] values = SplitLine(line, delimiter);
            if (TryReadPoint(values, xIndex, yIndex, out Point point))
            {
                points.Add(point);
            }
        }

        return points;
    }

    /// <summary>
    /// 读取首个非空文本行。
    /// </summary>
    /// <param name="reader">文件读取器。</param>
    /// <returns>首个非空行；不存在则返回 null。</returns>
    private static string? ReadFirstNonEmptyLine(StreamReader reader)
    {
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>
    /// 检测 CSV 分隔符。
    /// </summary>
    /// <param name="line">样本行。</param>
    /// <returns>推断到的分隔符。</returns>
    private static char DetectDelimiter(string line)
    {
        if (line.Contains('\t'))
        {
            return '\t';
        }

        if (line.Contains(';'))
        {
            return ';';
        }

        return ',';
    }

    /// <summary>
    /// 用分隔符拆分文本。
    /// </summary>
    /// <param name="line">原始文本行。</param>
    /// <param name="delimiter">分隔符。</param>
    /// <returns>拆分结果。</returns>
    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter, StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 从表头中解析 X/Y 列索引。
    /// </summary>
    /// <param name="headers">表头字段。</param>
    /// <returns>X/Y 列索引元组。</returns>
    private static (int xIndex, int yIndex) ResolveCoordinateIndexes(IReadOnlyList<string> headers)
    {
        int xIndex = -1;
        int yIndex = -1;

        for (int index = 0; index < headers.Count; index++)
        {
            string normalized = headers[index].Trim().ToLowerInvariant();
            if (xIndex < 0 && (normalized == "x" || normalized.EndsWith("x")))
            {
                xIndex = index;
                continue;
            }

            if (yIndex < 0 && (normalized == "y" || normalized.EndsWith("y")))
            {
                yIndex = index;
            }
        }

        if (xIndex < 0 || yIndex < 0)
        {
            throw new InvalidOperationException("CSV 表头未找到 x/y 列。");
        }

        return (xIndex, yIndex);
    }

    /// <summary>
    /// 从一行数据中读取二维点。
    /// </summary>
    /// <param name="values">行字段。</param>
    /// <param name="xIndex">X 列索引。</param>
    /// <param name="yIndex">Y 列索引。</param>
    /// <param name="point">解析结果点。</param>
    /// <returns>解析成功返回 true。</returns>
    private static bool TryReadPoint(string[] values, int xIndex, int yIndex, out Point point)
    {
        point = default;
        int maxIndex = Math.Max(xIndex, yIndex);
        if (values.Length <= maxIndex)
        {
            return false;
        }

        if (!TryParseDouble(values[xIndex], out double x) ||
            !TryParseDouble(values[yIndex], out double y))
        {
            return false;
        }

        if (double.IsNaN(x) || double.IsInfinity(x) ||
            double.IsNaN(y) || double.IsInfinity(y))
        {
            return false;
        }

        point = new Point(x, y);
        return true;
    }

    /// <summary>
    /// 使用 InvariantCulture 和 CurrentCulture 尝试解析浮点数。
    /// </summary>
    /// <param name="value">输入文本。</param>
    /// <param name="result">解析结果。</param>
    /// <returns>解析成功返回 true。</returns>
    private static bool TryParseDouble(string value, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result);
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
