using System;

namespace GrindCar.Models.Rail;

public sealed class RailProfileFitResult
{
    private readonly Func<double, double> _function;

    public RailProfileFitResult(Func<double, double> function, double minX, double maxX)
    {
        _function = function ?? throw new ArgumentNullException(nameof(function));

        if (maxX < minX)
        {
            throw new ArgumentOutOfRangeException(nameof(maxX), "拟合定义域无效。");
        }

        MinX = minX;
        MaxX = maxX;
    }

    public double MinX { get; }

    public double MaxX { get; }

    public Func<double, double> Function => Evaluate;

    public bool Contains(double x)
    {
        return x >= MinX && x <= MaxX;
    }

    public double Evaluate(double x)
    {
        if (!Contains(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"x={x} 超出拟合函数有效定义域 [{MinX}, {MaxX}]。");
        }

        return _function(x);
    }
}
