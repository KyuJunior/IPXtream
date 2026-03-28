using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPXtream.Models;
using IPXtream.Services;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;

namespace IPXtream.ViewModels;

/// <summary>
/// ViewModel for <see cref="Views.PlayerWindow"/>.
/// Owns the LibVLC Media and MediaPlayer lifetimes; the View's VideoView
/// binds its MediaPlayer property directly to <see cref="MediaPlayer"/>.
/// </summary>
public partial class PlayerViewModel : ObservableObject, IDisposable
{
    // â”€â”€ LibVLC core (shared per-process) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // LibVLC must be initialised on the UI thread; we do so in the View ctor.
    private LibVLC?      _libVlc;
    private Media?       _media;

    // â”€â”€ Public MediaPlayer (bound to VideoView in XAML) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private MediaPlayer? _mediaPlayer;

    // â”€â”€ Playback state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private bool   _isPlaying;
    [ObservableProperty] private bool   _isBuffering;
    [ObservableProperty] private string _statusText  = "Connectingâ€¦";
    [ObservableProperty] private string _errorText   = string.Empty;

    // â”€â”€ Volume (0â€“100) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private int _volume = 80;

    partial void OnVolumeChanged(int value)
    {
        if (MediaPlayer is not null)
            MediaPlayer.Volume = value;
    }

    // â”€â”€ Timeline / Seekbar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private float _position;
    [ObservableProperty] private string _positionText = "00:00:00";
    [ObservableProperty] private string _lengthText = "00:00:00";
    [ObservableProperty] private bool _isSeekable;

    /// <summary>Set to true by the View when the user is actively dragging the slider, to prevent stutter.</summary>
    public bool IsUserSeeking { get; set; }

    public void CommitSeek(float targetPosition)
    {
        if (MediaPlayer is not null)
        {
            MediaPlayer.Position = targetPosition;
            // Instantly update UI text so it doesn't freeze until next VLC tick
            PositionText = TimeSpan.FromMilliseconds(MediaPlayer.Length * targetPosition).ToString(@"hh\:mm\:ss");
        }
    }

    // â”€â”€ Tracks (Audio / Subtitles) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private TrackDescription[] _audioTracks = Array.Empty<TrackDescription>();
    [ObservableProperty] private TrackDescription[] _subtitleTracks = Array.Empty<TrackDescription>();

    private TrackDescription? _selectedAudioTrack;
    public TrackDescription? SelectedAudioTrack
    {
        get => _selectedAudioTrack;
        set
        {
            if (SetProperty(ref _selectedAudioTrack, value))
            {
                if (MediaPlayer != null && value.HasValue)
                    MediaPlayer.SetAudioTrack(value.Value.Id);
            }
        }
    }

    private TrackDescription? _selectedSubtitleTrack;
    public TrackDescription? SelectedSubtitleTrack
    {
        get => _selectedSubtitleTrack;
        set
        {
            if (SetProperty(ref _selectedSubtitleTrack, value))
            {
                if (MediaPlayer != null && value.HasValue)
                    MediaPlayer.SetSpu(value.Value.Id);
            }
        }
    }

    // â”€â”€ UI state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private bool _controlsVisible = true;
    [ObservableProperty] private bool _isFullscreen;

    // â”€â”€ Stream metadata (shown in the title bar / overlay) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string StreamTitle   { get; }
    public string StreamIconUrl { get; }

    // â”€â”€ Back-reference so the "Back" button can show the Dashboard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly DashboardViewModel _dashboardVm;

    // â”€â”€ Event: close player â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public event Action?  CloseRequested;

    // â”€â”€ Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly StreamItem      _stream;
    private readonly XtreamApiService _api;       // only needed for credentials

    /// <summary>Exposed so PlayerWindow code-behind can build the stream URL.</summary>
    public StreamItem Stream => _stream;

    // â”€â”€ Constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public PlayerViewModel(
        XtreamApiService   api,
        StreamItem         stream,
        DashboardViewModel dashboardVm)
    {
        _api          = api;
        _stream       = stream;
        _dashboardVm  = dashboardVm;
        StreamTitle   = stream.Name;
        StreamIconUrl = stream.StreamIcon;
    }

    // â”€â”€ Called by the View after LibVLC is initialised â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Initialises the <see cref="MediaPlayer"/> and begins playback.
    /// Must be called from the UI thread, after the View is loaded.
    /// </summary>
    public void Initialise(LibVLC libVlc, string streamUrl)
    {
        _libVlc = libVlc;

        var player = new MediaPlayer(libVlc) { Volume = Volume };

        // â”€â”€ Wire up playback events (all marshalled to UI thread) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        player.Playing   += (_, _) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            IsPlaying   = true;
            IsBuffering = false;
            StatusText  = string.Empty;
            ErrorText   = string.Empty;

            // Extract Audio and Subtitle tracks now that playback started
            if (MediaPlayer != null)
            {
                AudioTracks = MediaPlayer.AudioTrackDescription;
                SubtitleTracks = MediaPlayer.SpuDescription;

                var aid = MediaPlayer.AudioTrack;
                SelectedAudioTrack = AudioTracks?.FirstOrDefault(t => t.Id == aid);

                var sid = MediaPlayer.Spu;
                SelectedSubtitleTrack = SubtitleTracks?.FirstOrDefault(t => t.Id == sid);
            }
        });

        player.LengthChanged += (_, e) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            IsSeekable = e.Length > 0;
            if (IsSeekable)
                LengthText = TimeSpan.FromMilliseconds(e.Length).ToString(@"hh\:mm\:ss");
        });

        player.TimeChanged += (_, e) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            if (!IsUserSeeking && MediaPlayer != null && MediaPlayer.Length > 0)
            {
                Position = (float)e.Time / MediaPlayer.Length;
                PositionText = TimeSpan.FromMilliseconds(e.Time).ToString(@"hh\:mm\:ss");
            }
        });

        player.Buffering += (_, e) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            IsBuffering = true;
            StatusText  = $"Bufferingâ€¦ {e.Cache:0}%";
        });

        player.Paused += (_, _) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            IsPlaying  = false;
            StatusText = "Paused";
        });

        player.Stopped += (_, _) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            IsPlaying   = false;
            IsBuffering = false;
            StatusText  = "Stopped";
        });

        player.EncounteredError += (_, _) => App.Current.Dispatcher.BeginInvoke(() =>
        {
            IsPlaying  = false;
            IsBuffering= false;
            ErrorText  = "Playback error. Stream may be offline or URL is invalid.";
            StatusText = string.Empty;
        });

        // Play immediately
        _media = new Media(libVlc, new Uri(streamUrl));
        player.Play(_media);

        MediaPlayer = player;   // triggers VideoView binding
    }

    // â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private void TogglePlay()
    {
        if (MediaPlayer is null) return;
        if (MediaPlayer.IsPlaying) MediaPlayer.Pause();
        else                       MediaPlayer.Play();
    }

    [RelayCommand]
    private void Stop()
    {
        MediaPlayer?.Stop();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (MediaPlayer is null) return;
        MediaPlayer.Mute = !MediaPlayer.Mute;
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    [RelayCommand]
    private void Back()
    {
        Stop();
        CloseRequested?.Invoke();
    }

    // â”€â”€ IDisposable â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void Dispose()
    {
        MediaPlayer?.Stop();
        MediaPlayer?.Dispose();
        _media?.Dispose();
        // Do NOT dispose _libVlc here â€” it is shared and owned by the View.
    }
}


