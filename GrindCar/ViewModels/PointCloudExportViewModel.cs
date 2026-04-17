using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GrindCar.Models.PointCloud;
using GrindCar.Services.PointCloud;

namespace GrindCar.ViewModels;

/// <summary>
/// 点云导出窗口 ViewModel。
/// </summary>
public sealed class PointCloudExportViewModel : INotifyPropertyChanged
{
    private readonly PointCloudExportService _pointCloudExportService = new();

    private PointCloudDeviceInfo? _selectedDevice;
    private PointCloudExportFormat _selectedExportFormat = PointCloudExportFormat.Ply;
    private string _outputPath = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PointCloudDeviceInfo> Devices { get; } = new();

    public PointCloudDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set => SetField(ref _selectedDevice, value);
    }

    public PointCloudExportFormat SelectedExportFormat
    {
        get => _selectedExportFormat;
        set => SetField(ref _selectedExportFormat, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        private set => SetField(ref _outputPath, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInteract));
            }
        }
    }

    public bool CanInteract => !IsBusy;

    public async Task RefreshDevicesAsync()
    {
        IsBusy = true;
        StatusText = "正在刷新设备列表...";

        try
        {
            IReadOnlyList<PointCloudDeviceInfo> devices = await Task
                .Run(() => _pointCloudExportService.GetDevices())
                .ConfigureAwait(true);

            Devices.Clear();
            for (int index = 0; index < devices.Count; index++)
            {
                Devices.Add(devices[index]);
            }

            SelectedDevice = Devices.FirstOrDefault();
            StatusText = Devices.Count == 0 ? "未发现可用设备。" : $"已发现 {Devices.Count} 台设备。";
        }
        catch (PointCloudSdkException ex)
        {
            Devices.Clear();
            SelectedDevice = null;
            StatusText = ex.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportAsync()
    {
        if (SelectedDevice == null)
        {
            throw new InvalidOperationException("请先选择设备。");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            throw new InvalidOperationException("请先选择导出路径。");
        }

        string serialNumber = SelectedDevice.SerialNumber;
        IsBusy = true;
        StatusText = $"正在导出设备 {serialNumber} 的点云数据...";

        try
        {
            await Task.Run(() => _pointCloudExportService.ExportPointCloud(
                serialNumber,
                OutputPath,
                SelectedExportFormat)).ConfigureAwait(true);

            StatusText = $"导出完成: {OutputPath}";
        }
        catch (PointCloudSdkException ex)
        {
            StatusText = ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            const string message = "导出点云时发生未处理异常。";
            StatusText = $"{message} {ex.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetOutputPath(string outputPath)
    {
        OutputPath = outputPath;
        StatusText = $"已选择导出路径: {outputPath}";
    }

    public string GetDialogFilter()
    {
        return SelectedExportFormat switch
        {
            PointCloudExportFormat.Csv => "CSV 文件|*.csv",
            PointCloudExportFormat.Obj => "OBJ 文件|*.obj",
            _ => "PLY 文件|*.ply"
        };
    }

    public string GetDefaultExtension()
    {
        return SelectedExportFormat switch
        {
            PointCloudExportFormat.Csv => ".csv",
            PointCloudExportFormat.Obj => ".obj",
            _ => ".ply"
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
