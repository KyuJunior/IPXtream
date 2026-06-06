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
    // ── Flyleaf Player ────────────────────────────────────────────────────────
    public Player Player { get; }

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
        get => Player.Audio.Volume;
        set { Player.Audio.Volume = value; OnPropertyChanged(); }
    }

    // ── Track Collections (populated after stream opens) ───────────────────────
    public record SubtitleOption(string Name, SubtitlesStream? Value);

    public ObservableCollection<AudioStream>     AudioStreams    => Player.Audio.Streams;
    public IEnumerable<SubtitleOption> DisplaySubtitleStreams
    {
        get
        {
            yield return new SubtitleOption("(None)", null);
            if (Player.Subtitles.Streams != null)
                foreach (var s in Player.Subtitles.Streams)
                    yield return new SubtitleOption($"{s.Language} ({s.Codec})", s);
        }
    }

    public AudioStream? SelectedAudioStream
    {
        get => Player.Audio.Streams?.Count > 0
                ? Player.Audio.Streams.FirstOrDefault(s => s.StreamIndex == Player.Audio.StreamIndex)
                : null;
        set
        {
            if (value != null)
                Task.Run(() => Player.OpenAsync(value));
        }
    }

    public SubtitlesStream? SelectedSubtitleStream
    {
        get => Player.Subtitles.Streams?.Count > 0
                ? Player.Subtitles.Streams.FirstOrDefault(s => s.StreamIndex == Player.Subtitles.StreamIndex)
                : null;
        set
        {
            if (value != null)
                Task.Run(() => Player.OpenAsync(value));
            else
                Task.Run(() => Player.OpenAsync((SubtitlesStream?)null));

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
        XtreamApiService   api,
        StreamItem         stream,
        DashboardViewModel dashboardVm)
    {
        StreamTitle   = stream.Name;
        StreamIconUrl = stream.StreamIcon;

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
        Player.OpenAsync(streamUrl);
    }

    // ── Flyleaf INPC handler ──────────────────────────────────────────────────
    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            if (e.PropertyName == nameof(Player.Status))
                SyncStatus();
            else if (e.PropertyName == nameof(Player.Speed))
                OnPropertyChanged(nameof(SelectedSpeed));
        });
    }

    private void SyncStatus()
    {
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

        long cur = Player.CurTime;
        long dur = Player.Duration;

        IsSeekable   = dur > 0;
        Position     = dur > 0 ? (float)((double)cur / dur) : 0f;
        PositionText = TimeSpan.FromTicks(cur).ToString(@"hh\:mm\:ss");
        LengthText   = TimeSpan.FromTicks(dur).ToString(@"hh\:mm\:ss");
    }

    public void CommitSeek(float targetPosition)
    {
        if (Player.Duration > 0)
        {
            long targetTicks = (long)(Player.Duration * targetPosition);
            Player.Seek((int)(targetTicks / TimeSpan.TicksPerMillisecond));
        }
    }

    [RelayCommand]
    private void SkipForward()
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

    [RelayCommand]
    private void SkipBackward()
    {
        if (Player.Duration > 0)
        {
            long currentTicks = Player.CurTime;
            long stepTicks = 50000000; // 5 seconds
            long targetTicks = Math.Max(currentTicks - stepTicks, 0);
            Player.Seek((int)(targetTicks / TimeSpan.TicksPerMillisecond));
        }
    }

    // ── Speeds ────────────────────────────────────────────────────────────────
    public double[] PlaybackSpeeds { get; } = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

    public double SelectedSpeed
    {
        get => Player.Speed;
        set
        {
            Player.Speed = value;
            OnPropertyChanged();
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void TogglePlay()
    {
        if (Player.IsPlaying)
            Player.Pause();
        else
            Player.Play();
    }

    [RelayCommand]
    private void Stop()
    {
        Player.Stop();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void ToggleMute() => Player.Audio.Mute = !Player.Audio.Mute;

    [RelayCommand]
    private void Close()
    {
        Player.Stop();
        CloseRequested?.Invoke();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _positionTimer.Stop();
        Player.PropertyChanged -= OnPlayerPropertyChanged;

        System.Threading.Tasks.Task.Run(() =>
        {
            Player.Stop();
            Player.Dispose();
        });
    }
}
