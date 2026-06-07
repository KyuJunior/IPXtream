using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using IPXtream.Models;
using IPXtream.Services;

namespace IPXtream.ViewModels;

public record VLCSubtitleTrackProxy(int Id);
public record SubtitleOption(string Name, VLCSubtitleTrackProxy? Value);

public class VLCAudioTrackProxy
{
    public int Id { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the embedded VLC player.
/// </summary>
public partial class PlayerViewModel : ObservableObject, IDisposable
{
    // ── LibVLC Player ────────────────────────────────────────────────────────
    private readonly LibVLC _libVLC;
    public MediaPlayer MediaPlayer { get; }

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
        get => MediaPlayer?.Volume ?? 100;
        set 
        { 
            if (MediaPlayer != null)
            {
                MediaPlayer.Volume = value; 
                OnPropertyChanged(); 
            }
        }
    }

    // ── Track Collections (populated after stream opens) ───────────────────────
    public ObservableCollection<VLCAudioTrackProxy> AudioStreams { get; } = new();
    public ObservableCollection<SubtitleOption> DisplaySubtitleStreams { get; } = new();

    public VLCAudioTrackProxy? SelectedAudioStream
    {
        get => AudioStreams.FirstOrDefault(s => s.Id == MediaPlayer.AudioTrack);
        set
        {
            if (value != null && MediaPlayer.AudioTrack != value.Id)
            {
                MediaPlayer.SetAudioTrack(value.Id);
                OnPropertyChanged();
            }
        }
    }

    public VLCSubtitleTrackProxy? SelectedSubtitleStream
    {
        get
        {
            var spu = MediaPlayer.Spu;
            if (spu == -1) return null;
            return new VLCSubtitleTrackProxy(spu);
        }
        set
        {
            int targetId = value?.Id ?? -1;
            if (MediaPlayer.Spu != targetId)
            {
                MediaPlayer.SetSpu(targetId);
                OnPropertyChanged();
            }
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

        // Initialize LibVLC
        Core.Initialize();
        _libVLC = new LibVLC("--quiet", "--no-video-title-show");
        MediaPlayer = new MediaPlayer(_libVLC);

        // Attach events
        MediaPlayer.Opening += (s, e) => SyncStatus(VLCState.Opening);
        MediaPlayer.Buffering += (s, e) => SyncStatus(VLCState.Buffering, e.Cache);
        MediaPlayer.Playing += (s, e) => SyncStatus(VLCState.Playing, 100f);
        MediaPlayer.Paused += (s, e) => SyncStatus(VLCState.Paused);
        MediaPlayer.Stopped += (s, e) => SyncStatus(VLCState.Stopped);
        MediaPlayer.EndReached += (s, e) => SyncStatus(VLCState.Ended);
        MediaPlayer.EncounteredError += (s, e) => SyncStatus(VLCState.Error);

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
        LogService.Log($"Player Initialise: Opening stream: {streamUrl}");

        Media media;
        if (streamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            media = new Media(_libVLC, new Uri(streamUrl));
            media.AddOption("http-user-agent=IPXtream/1.0 (Windows; WPF)");
        }
        else
        {
            media = new Media(_libVLC, streamUrl, FromType.FromPath);
        }

        MediaPlayer.Play(media);
    }

    private void SyncStatus(VLCState state, float bufferPercent = 0f)
    {
        LogService.Log($"Player SyncStatus: {StreamTitle} state changed to {state} (Percent={bufferPercent})");

        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsPlaying   = state == VLCState.Playing;
            IsBuffering = state == VLCState.Opening || (state == VLCState.Buffering && bufferPercent < 100f);
            IsSeekable  = MediaPlayer.Length > 0;

            StatusText = state switch
            {
                VLCState.Opening   => "Connecting…",
                VLCState.Buffering => $"Buffering… {(int)bufferPercent}%",
                VLCState.Paused    => "Paused",
                VLCState.Ended     => "Ended",
                _                  => string.Empty
            };

            if (state == VLCState.Buffering && bufferPercent >= 100f)
            {
                StatusText = string.Empty;
            }

            if (state == VLCState.Playing || state == VLCState.Paused || state == VLCState.Opening || state == VLCState.Buffering)
                ErrorText = string.Empty;
            else if (state == VLCState.Error)
            {
                ErrorText = "Playback error. Stream may be offline or URL is invalid.";
                LogService.Log($"Player Playback Error: {StreamTitle} failed to load.");
            }

            if (state == VLCState.Playing)
                _positionTimer.Start();
            else
                _positionTimer.Stop();

            RefreshTimeline();
        });
    }

    private void RefreshTimeline()
    {
        if (IsUserSeeking) return;

        long cur = MediaPlayer.Time;
        long dur = MediaPlayer.Length;

        IsSeekable   = dur > 0;
        Position     = dur > 0 ? (float)((double)cur / dur) : 0f;
        PositionText = TimeSpan.FromMilliseconds(cur).ToString(@"hh\:mm\:ss");
        LengthText   = TimeSpan.FromMilliseconds(dur).ToString(@"hh\:mm\:ss");

        // Lazy load streams if they are empty
        if (AudioStreams.Count == 0 && MediaPlayer.AudioTrackDescription?.Length > 0)
        {
            UpdateAudioStreams();
        }
        if (DisplaySubtitleStreams.Count <= 1 && MediaPlayer.SpuDescription?.Length > 0)
        {
            UpdateSubtitleStreams();
        }
    }

    private void UpdateAudioStreams()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            AudioStreams.Clear();
            foreach (var track in MediaPlayer.AudioTrackDescription)
            {
                if (track.Id == -1) continue;
                AudioStreams.Add(new VLCAudioTrackProxy
                {
                    Id = track.Id,
                    Language = track.Name,
                    Codec = "Track"
                });
            }
            OnPropertyChanged(nameof(AudioStreams));
            OnPropertyChanged(nameof(SelectedAudioStream));
        });
    }

    private void UpdateSubtitleStreams()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            DisplaySubtitleStreams.Clear();
            DisplaySubtitleStreams.Add(new SubtitleOption("(None)", null));
            foreach (var track in MediaPlayer.SpuDescription)
            {
                if (track.Id == -1) continue;
                DisplaySubtitleStreams.Add(new SubtitleOption(track.Name, new VLCSubtitleTrackProxy(track.Id)));
            }
            OnPropertyChanged(nameof(DisplaySubtitleStreams));
            OnPropertyChanged(nameof(SelectedSubtitleStream));
        });
    }

    public void CommitSeek(float targetPosition)
    {
        if (MediaPlayer.Length > 0)
        {
            MediaPlayer.Position = targetPosition;
        }
    }

    [RelayCommand]
    private void SkipForward()
    {
        if (MediaPlayer.Length > 0)
        {
            long currentMs = MediaPlayer.Time;
            long maxMs = MediaPlayer.Length;
            long stepMs = 5000; // 5 seconds
            MediaPlayer.Time = Math.Min(currentMs + stepMs, maxMs);
        }
    }

    [RelayCommand]
    private void SkipBackward()
    {
        if (MediaPlayer.Length > 0)
        {
            long currentMs = MediaPlayer.Time;
            long stepMs = 5000; // 5 seconds
            MediaPlayer.Time = Math.Max(currentMs - stepMs, 0);
        }
    }

    // ── Speeds ────────────────────────────────────────────────────────────────
    public double[] PlaybackSpeeds { get; } = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

    public double SelectedSpeed
    {
        get => MediaPlayer != null ? MediaPlayer.Rate : 1.0;
        set
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.SetRate((float)value);
                OnPropertyChanged();
            }
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void TogglePlay()
    {
        if (MediaPlayer.IsPlaying)
            MediaPlayer.Pause();
        else
            MediaPlayer.Play();
    }

    [RelayCommand]
    private void Stop()
    {
        MediaPlayer.Stop();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void ToggleMute()
    {
        if (MediaPlayer != null)
        {
            MediaPlayer.Mute = !MediaPlayer.Mute;
            OnPropertyChanged(nameof(Volume));
        }
    }

    [RelayCommand]
    private void Close()
    {
        MediaPlayer.Stop();
        CloseRequested?.Invoke();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _positionTimer.Stop();

        System.Threading.Tasks.Task.Run(() =>
        {
            MediaPlayer.Stop();
            MediaPlayer.Dispose();
            _libVLC.Dispose();
        });
    }
}
