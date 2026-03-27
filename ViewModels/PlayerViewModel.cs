using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPXtream.Models;
using IPXtream.Services;
using LibVLCSharp.Shared;

namespace IPXtream.ViewModels;

/// <summary>
/// ViewModel for <see cref="Views.PlayerWindow"/>.
/// Owns the LibVLC Media and MediaPlayer lifetimes; the View's VideoView
/// binds its MediaPlayer property directly to <see cref="MediaPlayer"/>.
/// </summary>
public partial class PlayerViewModel : ObservableObject, IDisposable
{
    // ── LibVLC core (shared per-process) ──────────────────────────────────────
    // LibVLC must be initialised on the UI thread; we do so in the View ctor.
    private LibVLC?      _libVlc;
    private Media?       _media;

    // ── Public MediaPlayer (bound to VideoView in XAML) ───────────────────────
    [ObservableProperty] private MediaPlayer? _mediaPlayer;

    // ── Playback state ────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isPlaying;
    [ObservableProperty] private bool   _isBuffering;
    [ObservableProperty] private string _statusText  = "Connecting…";
    [ObservableProperty] private string _errorText   = string.Empty;

    // ── Volume (0–100) ────────────────────────────────────────────────────────
    [ObservableProperty] private int _volume = 80;

    partial void OnVolumeChanged(int value)
    {
        if (MediaPlayer is not null)
            MediaPlayer.Volume = value;
    }

    // ── UI state ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _controlsVisible = true;
    [ObservableProperty] private bool _isFullscreen;

    // ── Stream metadata (shown in the title bar / overlay) ───────────────────
    public string StreamTitle   { get; }
    public string StreamIconUrl { get; }

    // ── Back-reference so the "Back" button can show the Dashboard ───────────
    private readonly DashboardViewModel _dashboardVm;

    // ── Event: close player ───────────────────────────────────────────────────
    public event Action?  CloseRequested;

    // ── Data ──────────────────────────────────────────────────────────────────
    private readonly StreamItem      _stream;
    private readonly XtreamApiService _api;       // only needed for credentials

    /// <summary>Exposed so PlayerWindow code-behind can build the stream URL.</summary>
    public StreamItem Stream => _stream;

    // ── Constructor ───────────────────────────────────────────────────────────
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

    // ── Called by the View after LibVLC is initialised ────────────────────────

    /// <summary>
    /// Initialises the <see cref="MediaPlayer"/> and begins playback.
    /// Must be called from the UI thread, after the View is loaded.
    /// </summary>
    public void Initialise(LibVLC libVlc, string streamUrl)
    {
        _libVlc = libVlc;

        var player = new MediaPlayer(libVlc) { Volume = Volume };

        // ── Wire up playback events (all marshalled to UI thread) ─────────────
        player.Playing   += (_, _) => App.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying   = true;
            IsBuffering = false;
            StatusText  = string.Empty;
            ErrorText   = string.Empty;
        });

        player.Buffering += (_, e) => App.Current.Dispatcher.Invoke(() =>
        {
            IsBuffering = true;
            StatusText  = $"Buffering… {e.Cache:0}%";
        });

        player.Paused += (_, _) => App.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying  = false;
            StatusText = "Paused";
        });

        player.Stopped += (_, _) => App.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying   = false;
            IsBuffering = false;
            StatusText  = "Stopped";
        });

        player.EncounteredError += (_, _) => App.Current.Dispatcher.Invoke(() =>
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

    // ── Commands ──────────────────────────────────────────────────────────────

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

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        MediaPlayer?.Stop();
        MediaPlayer?.Dispose();
        _media?.Dispose();
        // Do NOT dispose _libVlc here — it is shared and owned by the View.
    }
}
