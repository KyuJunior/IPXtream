using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPXtream.Models;
using IPXtream.Services;

namespace IPXtream.ViewModels;

public record SubtitleOption(string Name, object? Value);

public class VLCAudioTrackProxy
{
    public int Id { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the embedded MediaElement player.
/// </summary>
public partial class PlayerViewModel : ObservableObject, IDisposable
{
    // ── Observable playback state ─────────────────────────────────────────────
    [ObservableProperty] private bool   _isPlaying;
    [ObservableProperty] private bool   _isBuffering;
    [ObservableProperty] private bool   _isSeekable;
    [ObservableProperty] private string _statusText  = string.Empty;
    [ObservableProperty] private string _errorText   = string.Empty;

    // ── Timeline ──────────────────────────────────────────────────────────────
    [ObservableProperty] private float  _position;
    [ObservableProperty] private string _positionText = "00:00:00";
    [ObservableProperty] private string _lengthText   = "00:00:00";

    // ── Volume ────────────────────────────────────────────────────────────────
    private int _volume = 100;
    public int Volume
    {
        get => _volume;
        set 
        { 
            if (_volume != value)
            {
                _volume = value; 
                OnPropertyChanged(); 
                SetVolumeAction?.Invoke(value / 100.0);
            }
        }
    }

    // ── Track Collections (empty placeholders for compatibility) ──────────────
    public ObservableCollection<VLCAudioTrackProxy> AudioStreams { get; } = new();
    public ObservableCollection<SubtitleOption> DisplaySubtitleStreams { get; } = new();

    public VLCAudioTrackProxy? SelectedAudioStream
    {
        get => null;
        set { }
    }

    public object? SelectedSubtitleStream
    {
        get => null;
        set { }
    }

    // ── UI state ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _controlsVisible = true;
    [ObservableProperty] private bool _isFullscreen;

    public bool IsUserSeeking { get; set; }

    // ── Stream metadata ───────────────────────────────────────────────────────
    public string StreamTitle   { get; }
    public string StreamIconUrl { get; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action? CloseRequested;

    // ── Internals ─────────────────────────────────────────────────────────────
    private readonly DispatcherTimer _positionTimer;

    // ── Delegates for View Interaction ────────────────────────────────────────
    public Action? PlayAction;
    public Action? PauseAction;
    public Action? StopAction;
    public Action<TimeSpan>? SeekAction;
    public Action<double>? SetVolumeAction; // 0.0 to 1.0
    public Action<bool>? SetMuteAction;
    public Action<double>? SetSpeedAction;
    public Action<string>? OpenUrlAction;

    public Func<TimeSpan>? GetCurrentPositionFunc;
    public Func<TimeSpan>? GetDurationFunc;

    public PlayerViewModel(
        XtreamApiService   api,
        StreamItem         stream,
        DashboardViewModel dashboardVm)
    {
        StreamTitle   = stream.Name;
        StreamIconUrl = stream.StreamIcon;

        // Default item for subtitles
        DisplaySubtitleStreams.Add(new SubtitleOption("(None)", null));

        // Timer drives the seekbar; fires every 50ms on the UI thread for a smooth glide
        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _positionTimer.Tick += (_, _) => RefreshTimeline();
    }

    public void Initialise(string streamUrl)
    {
        ErrorText = string.Empty;
        IsBuffering = true;
        StatusText = "Connecting…";
        LogService.Log($"Player Initialise: Opening stream: {streamUrl}");

        OpenUrlAction?.Invoke(streamUrl);
    }

    public void OnMediaOpened()
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsPlaying = true;
            IsBuffering = false;
            StatusText = string.Empty;
            ErrorText = string.Empty;

            var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
            IsSeekable = dur > TimeSpan.Zero;

            _positionTimer.Start();
            RefreshTimeline();
        });
    }

    public void OnMediaFailed(string errorMsg)
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsPlaying = false;
            IsBuffering = false;
            _positionTimer.Stop();
            ErrorText = errorMsg;
            StatusText = string.Empty;
            LogService.Log($"Player Playback Error: {StreamTitle} failed to load. Reason: {errorMsg}");
        });
    }

    public void OnMediaEnded()
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsPlaying = false;
            IsBuffering = false;
            _positionTimer.Stop();
            StatusText = "Ended";
            CloseRequested?.Invoke();
        });
    }

    public void OnBufferingStarted()
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsBuffering = true;
            StatusText = "Buffering…";
        });
    }

    public void OnBufferingEnded()
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsBuffering = false;
            StatusText = string.Empty;
        });
    }

    private void RefreshTimeline()
    {
        if (IsUserSeeking) return;

        var cur = GetCurrentPositionFunc?.Invoke() ?? TimeSpan.Zero;
        var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;

        IsSeekable   = dur > TimeSpan.Zero;
        Position     = dur > TimeSpan.Zero ? (float)(cur.TotalMilliseconds / dur.TotalMilliseconds) : 0f;
        PositionText = cur.ToString(@"hh\:mm\:ss");
        LengthText   = dur.ToString(@"hh\:mm\:ss");
    }

    public void CommitSeek(float targetPosition)
    {
        var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
        if (dur > TimeSpan.Zero)
        {
            var targetTime = TimeSpan.FromMilliseconds(dur.TotalMilliseconds * targetPosition);
            SeekAction?.Invoke(targetTime);
        }
    }

    [RelayCommand]
    private void SkipForward()
    {
        var cur = GetCurrentPositionFunc?.Invoke() ?? TimeSpan.Zero;
        var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
        if (dur > TimeSpan.Zero)
        {
            var targetTime = TimeSpan.FromMilliseconds(Math.Min(cur.TotalMilliseconds + 5000, dur.TotalMilliseconds));
            SeekAction?.Invoke(targetTime);
        }
    }

    [RelayCommand]
    private void SkipBackward()
    {
        var cur = GetCurrentPositionFunc?.Invoke() ?? TimeSpan.Zero;
        var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
        if (dur > TimeSpan.Zero)
        {
            var targetTime = TimeSpan.FromMilliseconds(Math.Max(cur.TotalMilliseconds - 5000, 0));
            SeekAction?.Invoke(targetTime);
        }
    }

    // ── Speeds ────────────────────────────────────────────────────────────────
    public double[] PlaybackSpeeds { get; } = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

    private double _selectedSpeed = 1.0;
    public double SelectedSpeed
    {
        get => _selectedSpeed;
        set
        {
            if (Math.Abs(_selectedSpeed - value) > 0.001)
            {
                _selectedSpeed = value;
                OnPropertyChanged();
                SetSpeedAction?.Invoke(value);
            }
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            PauseAction?.Invoke();
            IsPlaying = false;
        }
        else
        {
            PlayAction?.Invoke();
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        StopAction?.Invoke();
        IsPlaying = false;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        SetMuteAction?.Invoke(IsMuted);
    }

    [ObservableProperty] private bool _isMuted;

    [RelayCommand]
    private void Close()
    {
        StopAction?.Invoke();
        IsPlaying = false;
        CloseRequested?.Invoke();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _positionTimer.Stop();
        StopAction?.Invoke();
    }
}
