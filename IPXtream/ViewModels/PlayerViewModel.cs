using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using FlyleafLib.MediaFramework.MediaStream;
using IPXtream.Models;
using IPXtream.Services;

namespace IPXtream.ViewModels;

/// <summary>
/// ViewModel for the embedded Flyleaf player.
/// The View's FlyleafHost.Player is set directly in code-behind; this VM owns the Player lifetime.
/// </summary>
public partial class PlayerViewModel : ObservableObject, IDisposable
{
    // ── Stream / Playlist Metadata ────────────────────────────────────────────
    public StreamItem CurrentStream { get; }
    public System.Collections.Generic.List<StreamItem> SiblingStreams { get; }
    private readonly DashboardViewModel _dashboardVm;

    public bool HasNextEpisode
    {
        get
        {
            if (CurrentStream == null || CurrentStream.StreamType != "series" || SiblingStreams == null || SiblingStreams.Count <= 1)
                return false;

            var index = SiblingStreams.FindIndex(s => s.StreamId == CurrentStream.StreamId);
            return index != -1 && index < SiblingStreams.Count - 1;
        }
    }

    // ── Player Engine ─────────────────────────────────────────────────────────
    public Player? Player { get; }
    public string SelectedPlayerEngine { get; }

    // ── Delegates for other player engines ────────────────────────────────────
    public Action? PlayAction { get; set; }
    public Action? PauseAction { get; set; }
    public Action? StopAction { get; set; }
    public Action<TimeSpan>? SeekAction { get; set; }
    public Action<double>? SetVolumeAction { get; set; }
    public Action<bool>? SetMuteAction { get; set; }
    public Action<double>? SetSpeedAction { get; set; }
    public Action<string>? OpenUrlAction { get; set; }
    public Func<TimeSpan>? GetCurrentPositionFunc { get; set; }
    public Func<TimeSpan>? GetDurationFunc { get; set; }

    [ObservableProperty] private bool _isMuted;
    private int _mediaElementVolume = 80;
    private double _selectedSpeed = 1.0;

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
    public int Volume
    {
        get => Player != null ? Player.Audio.Volume : _mediaElementVolume;
        set
        {
            if (Player != null)
            {
                Player.Audio.Volume = value;
            }
            else
            {
                _mediaElementVolume = value;
                SetVolumeAction?.Invoke(value);
            }
            OnPropertyChanged();
        }
    }

    // ── Track Collections (populated after stream opens) ───────────────────────
    public record SubtitleOption(string Name, SubtitlesStream? Value);

    public ObservableCollection<AudioStream> AudioStreams => Player?.Audio?.Streams ?? new();
    public IEnumerable<SubtitleOption> DisplaySubtitleStreams
    {
        get
        {
            yield return new SubtitleOption("(None)", null);
            if (Player?.Subtitles?.Streams != null)
                foreach (var s in Player.Subtitles.Streams)
                    yield return new SubtitleOption($"{s.Language} ({s.Codec})", s);
        }
    }

    public AudioStream? SelectedAudioStream
    {
        get => Player?.Audio?.Streams?.Count > 0
                ? Player.Audio.Streams.FirstOrDefault(s => s.StreamIndex == Player.Audio.StreamIndex)
                : null;
        set
        {
            if (Player != null && value != null)
                Task.Run(() => Player.OpenAsync(value));
        }
    }

    public SubtitlesStream? SelectedSubtitleStream
    {
        get => Player?.Subtitles?.Streams?.Count > 0
                ? Player.Subtitles.Streams.FirstOrDefault(s => s.StreamIndex == Player.Subtitles.StreamIndex)
                : null;
        set
        {
            if (Player != null)
            {
                if (value != null)
                    Task.Run(() => Player.OpenAsync(value));
                else
                    Task.Run(() => Player.OpenAsync((SubtitlesStream?)null));
            }

            OnPropertyChanged();
        }
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

    public PlayerViewModel(
        XtreamApiService                             api,
        StreamItem                                   stream,
        System.Collections.Generic.List<StreamItem> siblings,
        DashboardViewModel                           dashboardVm,
        string                                       playerEngine)
    {
        CurrentStream        = stream;
        SiblingStreams       = siblings ?? new System.Collections.Generic.List<StreamItem>();
        _dashboardVm         = dashboardVm;
        StreamTitle          = stream.Name;
        StreamIconUrl        = stream.StreamIcon;
        SelectedPlayerEngine = playerEngine;

        // Timer drives the seekbar; fires every 50ms on the UI thread for a smooth glide
        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _positionTimer.Tick += (_, _) => RefreshTimeline();

        if (SelectedPlayerEngine == "Flyleaf")
        {
            // Create player with a config that prevents it opening its own window
            var cfg = new Config();
            Player = new Player(cfg);

            // Subscribe to Flyleaf's own INPC (fires on its own thread — marshal to UI)
            Player.PropertyChanged += OnPlayerPropertyChanged;

            // Subscribe directly to Audio/Subtitles IsOpened — fires when Flyleaf
            // has finished populating the Streams collection (more reliable than Status.Playing)
            Player.Audio.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Player.Audio.IsOpened) ||
                    e.PropertyName == nameof(Player.Audio.StreamIndex))
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        OnPropertyChanged(nameof(AudioStreams));
                        OnPropertyChanged(nameof(SelectedAudioStream));
                    });
                }
            };

            Player.Subtitles.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Player.Subtitles.IsOpened) ||
                    e.PropertyName == nameof(Player.Subtitles.StreamIndex))
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        OnPropertyChanged(nameof(DisplaySubtitleStreams));
                        OnPropertyChanged(nameof(SelectedSubtitleStream));
                    });
                }
            };
        }
    }

    public void Initialise(string streamUrl)
    {
        ErrorText = string.Empty;
        if (Player != null)
        {
            Player.OpenAsync(streamUrl);
        }
        else
        {
            IsBuffering = true;
            StatusText = "Connecting…";
            OpenUrlAction?.Invoke(streamUrl);
        }
    }

    // ── Dynamic event callbacks for non-Flyleaf player engines ────────────────
    public void OnMediaOpened()
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsPlaying = true;
            IsBuffering = false;
            StatusText = string.Empty;
            ErrorText = string.Empty;
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

    // ── Flyleaf INPC handler ──────────────────────────────────────────────────
    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            if (Player != null)
            {
                if (e.PropertyName == nameof(Player.Status))
                    SyncStatus();
                else if (e.PropertyName == nameof(Player.Speed))
                    OnPropertyChanged(nameof(SelectedSpeed));
            }
        });
    }

    private void SyncStatus()
    {
        if (Player == null) return;
        var s = Player.Status;

        IsPlaying   = s == Status.Playing;
        IsBuffering = s == Status.Opening;
        IsSeekable  = Player.Duration > 0;

        StatusText = s switch
        {
            Status.Opening => "Connecting…",
            Status.Paused  => "Paused",
            Status.Ended   => "Ended",
            _              => string.Empty
        };

        if (s == Status.Playing || s == Status.Paused || s == Status.Opening)
            ErrorText = string.Empty;
        else if (s == Status.Failed)
            ErrorText = "Playback error. Stream may be offline or URL is invalid.";

        if (s == Status.Playing)
            _positionTimer.Start();
        else
            _positionTimer.Stop();

        RefreshTimeline();
    }

    private void RefreshTimeline()
    {
        if (IsUserSeeking) return;

        if (Player != null)
        {
            long cur = Player.CurTime;
            long dur = Player.Duration;

            IsSeekable   = dur > 0;
            Position     = dur > 0 ? (float)((double)cur / dur) : 0f;
            PositionText = TimeSpan.FromTicks(cur).ToString(@"hh\:mm\:ss");
            LengthText   = TimeSpan.FromTicks(dur).ToString(@"hh\:mm\:ss");
        }
        else
        {
            var cur = GetCurrentPositionFunc?.Invoke() ?? TimeSpan.Zero;
            var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;

            IsSeekable   = dur > TimeSpan.Zero;
            Position     = dur > TimeSpan.Zero ? (float)(cur.TotalMilliseconds / dur.TotalMilliseconds) : 0f;
            PositionText = cur.ToString(@"hh\:mm\:ss");
            LengthText   = dur.ToString(@"hh\:mm\:ss");
        }

        if (Position > 0 && Position < 0.99 && CurrentStream != null && CurrentStream.StreamType != "live")
        {
            _dashboardVm.UpdateStreamProgress(CurrentStream, Position * 100.0);
        }
    }

    public void CommitSeek(float targetPosition)
    {
        if (Player != null)
        {
            if (Player.Duration > 0)
            {
                long targetTicks = (long)(Player.Duration * targetPosition);
                Player.Seek((int)(targetTicks / TimeSpan.TicksPerMillisecond));
            }
        }
        else
        {
            var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
            if (dur > TimeSpan.Zero)
            {
                var targetTime = TimeSpan.FromMilliseconds(dur.TotalMilliseconds * targetPosition);
                SeekAction?.Invoke(targetTime);
            }
        }
    }

    [RelayCommand]
    private void SkipForward()
    {
        if (Player != null)
        {
            if (Player.Duration > 0)
            {
                long currentTicks = Player.CurTime;
                long maxTicks = Player.Duration;
                long stepTicks = 50000000; // 5 seconds (10,000 * 5,000)
                long targetTicks = Math.Min(currentTicks + stepTicks, maxTicks);
                Player.Seek((int)(targetTicks / TimeSpan.TicksPerMillisecond));
            }
        }
        else
        {
            var cur = GetCurrentPositionFunc?.Invoke() ?? TimeSpan.Zero;
            var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
            if (dur > TimeSpan.Zero)
            {
                var targetTime = TimeSpan.FromMilliseconds(Math.Min(cur.TotalMilliseconds + 5000, dur.TotalMilliseconds));
                SeekAction?.Invoke(targetTime);
            }
        }
    }

    [RelayCommand]
    private void SkipBackward()
    {
        if (Player != null)
        {
            if (Player.Duration > 0)
            {
                long currentTicks = Player.CurTime;
                long stepTicks = 50000000; // 5 seconds
                long targetTicks = Math.Max(currentTicks - stepTicks, 0);
                Player.Seek((int)(targetTicks / TimeSpan.TicksPerMillisecond));
            }
        }
        else
        {
            var cur = GetCurrentPositionFunc?.Invoke() ?? TimeSpan.Zero;
            var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
            if (dur > TimeSpan.Zero)
            {
                var targetTime = TimeSpan.FromMilliseconds(Math.Max(cur.TotalMilliseconds - 5000, 0));
                SeekAction?.Invoke(targetTime);
            }
        }
    }

    // ── Speeds ────────────────────────────────────────────────────────────────
    public double[] PlaybackSpeeds { get; } = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

    public double SelectedSpeed
    {
        get => Player != null ? Player.Speed : _selectedSpeed;
        set
        {
            if (Player != null)
            {
                Player.Speed = value;
            }
            else
            {
                _selectedSpeed = value;
                SetSpeedAction?.Invoke(value);
            }
            OnPropertyChanged();
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void TogglePlay()
    {
        if (Player != null)
        {
            if (Player.IsPlaying)
                Player.Pause();
            else
                Player.Play();
        }
        else
        {
            if (IsPlaying)
            {
                PauseAction?.Invoke();
                IsPlaying = false;
                StatusText = "Paused";
            }
            else
            {
                PlayAction?.Invoke();
                IsPlaying = true;
                StatusText = string.Empty;
                _positionTimer.Start();
            }
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (Player != null)
        {
            Player.Stop();
        }
        else
        {
            StopAction?.Invoke();
            IsPlaying = false;
        }
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void ToggleMute()
    {
        if (Player != null)
        {
            Player.Audio.Mute = !Player.Audio.Mute;
            IsMuted = Player.Audio.Mute;
            OnPropertyChanged(nameof(Volume));
        }
        else
        {
            IsMuted = !IsMuted;
            SetMuteAction?.Invoke(IsMuted);
        }
    }

    [RelayCommand]
    private void PlayNextEpisode()
    {
        if (!HasNextEpisode) return;

        var index = SiblingStreams.FindIndex(s => s.StreamId == CurrentStream.StreamId);
        if (index != -1 && index < SiblingStreams.Count - 1)
        {
            var nextEpisode = SiblingStreams[index + 1];
            _dashboardVm.TriggerPlayRequest(nextEpisode, SiblingStreams);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (Player != null)
        {
            Player.Stop();
        }
        else
        {
            StopAction?.Invoke();
            IsPlaying = false;
        }
        CloseRequested?.Invoke();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _positionTimer.Stop();
        if (Player != null)
        {
            Player.PropertyChanged -= OnPlayerPropertyChanged;

            System.Threading.Tasks.Task.Run(() =>
            {
                Player.Stop();
                Player.Dispose();
            });
        }
        else
        {
            StopAction?.Invoke();
        }
    }
}
