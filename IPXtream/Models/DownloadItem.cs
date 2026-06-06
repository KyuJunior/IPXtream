using CommunityToolkit.Mvvm.ComponentModel;

namespace IPXtream.Models;

public enum DownloadStatus { Queued, Downloading, Paused, Completed, Failed, Cancelled }

public partial class DownloadItem : ObservableObject
{
    public string Name      { get; init; } = string.Empty;
    public string Url       { get; init; } = string.Empty;
    public string Extension { get; init; } = "mp4";
    
    // Grouping & settings
    public string GroupName { get; set; } = string.Empty;
    public string? ShowName { get; set; }
    public bool IsAppUpdate { get; set; }

    [ObservableProperty] private double         _progress;
    [ObservableProperty] private string         _speedText  = string.Empty;
    [ObservableProperty] private string         _sizeText   = string.Empty;
    [ObservableProperty] private string         _statusText = "Queued";
    [ObservableProperty] private DownloadStatus _status     = DownloadStatus.Queued;
    [ObservableProperty] private string         _destPath   = string.Empty;
    [ObservableProperty] private int            _speedLimitKbps = 0; // 0 = no limit

    public CancellationTokenSource Cts { get; set; } = new();

    public bool IsActive    => Status is DownloadStatus.Queued or DownloadStatus.Downloading;
    public bool IsPaused    => Status == DownloadStatus.Paused;
    public bool IsCompleted => Status == DownloadStatus.Completed;

    partial void OnStatusChanged(DownloadStatus value)
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsPaused));
    }
}
