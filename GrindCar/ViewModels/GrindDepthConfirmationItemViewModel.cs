using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GrindCar.Services.Measurement;

namespace GrindCar.ViewModels;

/// <summary>
/// 打磨深度确认窗口中的单个角度编辑项。
/// </summary>
public sealed class GrindDepthConfirmationItemViewModel : INotifyPropertyChanged
{
    private string _confirmedDepthText;
    private double _confirmedDepth;
    private int _grindingTimes;
    private string _errorMessage = string.Empty;

    public GrindDepthConfirmationItemViewModel(MeasurementGrindingTimesResult source)
    {
        Angle = source.Angle;
        RegularAverageDepth = source.RegularAverageDepth;
        DefectDepth = source.DefectDepth;
        FinalGrindDepth = source.FinalGrindDepth;
        _confirmedDepth = source.FinalGrindDepth;
        _confirmedDepthText = source.FinalGrindDepth.ToString("0.######", CultureInfo.CurrentCulture);
        _grindingTimes = MeasurementParameterService.CalculateGrindingTimes(source.FinalGrindDepth);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Angle { get; }

    public double RegularAverageDepth { get; }

    public double DefectDepth { get; }

    public double FinalGrindDepth { get; }

    public double MeasuredAverageDepth => FinalGrindDepth;

    public string ConfirmedDepthText
    {
        get => _confirmedDepthText;
        set
        {
            if (_confirmedDepthText == value)
            {
                return;
            }

            _confirmedDepthText = value;
            ValidateConfirmedDepth();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfirmedDepth));
            OnPropertyChanged(nameof(GrindingTimes));
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasError));
        }
    }

    public double ConfirmedDepth => _confirmedDepth;

    public int GrindingTimes => _grindingTimes;

    public string ErrorMessage => _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public bool ValidateConfirmedDepth()
    {
        string input = _confirmedDepthText.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            _errorMessage = "确认打磨深度不能为空。";
            return false;
        }

        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out double depth) &&
            !double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out depth))
        {
            _errorMessage = "确认打磨深度格式无效。";
            return false;
        }

        try
        {
            _grindingTimes = MeasurementParameterService.CalculateGrindingTimes(depth);
        }
        catch (InvalidOperationException ex)
        {
            _errorMessage = ex.Message;
            return false;
        }

        _confirmedDepth = depth;
        _errorMessage = string.Empty;
        return true;
    }

    public MeasurementGrindingTimesResult ToConfirmedResult()
    {
        if (!ValidateConfirmedDepth())
        {
            throw new InvalidOperationException(_errorMessage);
        }

        return new MeasurementGrindingTimesResult(
            Angle,
            RegularAverageDepth,
            DefectDepth,
            ConfirmedDepth,
            GrindingTimes);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
