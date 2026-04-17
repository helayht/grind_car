using System;

namespace GrindCar.Services.Rail.Processing;

/// <summary>
/// 快速选择算法，用于选取第 k 小元素。
/// </summary>
internal static class QuickSelect
{
    public static double SelectKthSmallest(double[] values, int k)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Length == 0)
        {
            throw new ArgumentException("待选择数组不能为空。", nameof(values));
        }

        if (k < 0 || k >= values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        int left = 0;
        int right = values.Length - 1;

        while (true)
        {
            if (left == right)
            {
                return values[left];
            }

            int pivotIndex = left + (right - left) / 2;
            (int equalStart, int equalEnd) = Partition(values, left, right, pivotIndex);
            if (k < equalStart)
            {
                right = equalStart - 1;
                continue;
            }

            if (k > equalEnd)
            {
                left = equalEnd + 1;
                continue;
            }

            return values[k];
        }
    }

    private static (int equalStart, int equalEnd) Partition(double[] values, int left, int right, int pivotIndex)
    {
        double pivotValue = values[pivotIndex];
        Swap(values, pivotIndex, right);

        int less = left;
        int current = left;
        int greater = right;

        while (current <= greater)
        {
            if (values[current] < pivotValue)
            {
                Swap(values, less, current);
                less++;
                current++;
            }
            else if (values[current] > pivotValue)
            {
                Swap(values, current, greater);
                greater--;
            }
            else
            {
                current++;
            }
        }

        return (less, greater);
    }

    private static void Swap(double[] values, int left, int right)
    {
        if (left == right)
        {
            return;
        }

        (values[left], values[right]) = (values[right], values[left]);
    }
}