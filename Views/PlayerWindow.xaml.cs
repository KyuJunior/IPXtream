using System.Windows;
using System.Windows.Input;
using IPXtream.ViewModels;
using LibVLCSharp.Shared;

namespace IPXtream.Views;

public partial class PlayerWindow : Window
{
    // ── LibVLC instance — one per process is the recommended pattern ──────────
    // We keep it here because the PlayerWindow owns it; if multiple players
    // are ever opened, promote this to App-level.
    private static LibVLC? _libVlc;

    private readonly PlayerViewModel _vm;

    // Timer to auto-hide controls
    private readonly System.Windows.Threading.DispatcherTimer _hideTimer;
    private bool _controlsLocked; // keeps controls visible while mouse is over them

    public PlayerWindow(PlayerViewModel viewModel)
    {
        InitializeComponent();

        _vm      = viewModel;
        DataContext = _vm;

        _vm.CloseRequested     += () => Close();
        _vm.PropertyChanged    += OnVmPropertyChanged;

        // Auto-hide timer (3 seconds after mouse stops moving)
        _hideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hideTimer.Tick += (_, _) => HideControls();

        Loaded  += OnLoaded;
        Closed  += OnClosed;
        PreviewMouseMove += OnMouseMove;
    }

    // ── Startup: build stream URL and start VLC ───────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // LibVLC must be initialised AFTER the window handle exists
        Core.Initialize();                  // loads libvlc native DLLs
        _libVlc ??= new LibVLC(enableDebugLogs: false);

        // The PlayerViewModel needs credentials to build the URL.
        // We grab them from the singleton ApiService via reflection-free approach:
        // The DashboardViewModel stored them — but we receive the stream + api service.
        // Simplest: reconstruct URL here from credentials stored in App.
        // (We'll request the credentials from the VM via a public property set by DashboardWindow.)
        var streamUrl = BuildStreamUrl();
        _vm.Initialise(_libVlc, streamUrl);

        _hideTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _vm.Dispose();
        _hideTimer.Stop();
    }

    // ── Stream URL construction ───────────────────────────────────────────────
    /// <summary>
    /// Builds the correct playback URL based on stream type.
    /// Live:   {base}/live/{user}/{pass}/{id}.ts
    /// VOD:    {base}/movie/{user}/{pass}/{id}.{ext}
    /// Series: {base}/series/{user}/{pass}/{id}.{ext}
    /// </summary>
    private string BuildStreamUrl()
    {
        // Credentials are stored in App.Credentials set at login
        var c   = App.CurrentCredentials!;
        var s   = _vm.Stream;

        return s.StreamType switch
        {
            "movie"  => $"{c.BaseUrl}/movie/{c.Username}/{c.Password}/{s.EffectiveStreamId}.{s.ContainerExtension}",
            "series" => $"{c.BaseUrl}/series/{c.Username}/{c.Password}/{s.EffectiveStreamId}.{s.ContainerExtension}",
            _        => $"{c.BaseUrl}/live/{c.Username}/{c.Password}/{s.EffectiveStreamId}.{s.ContainerExtension}"
        };
    }

    // ── Fullscreen ────────────────────────────────────────────────────────────
    private void OnVmPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlayerViewModel.IsFullscreen)) return;

        if (_vm.IsFullscreen)
        {
            WindowStyle      = WindowStyle.None;
            WindowState      = WindowState.Maximized;
            ResizeMode       = ResizeMode.NoResize;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            ResizeMode  = ResizeMode.CanResize;
        }
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                _vm.TogglePlayCommand.Execute(null);
                break;
            case Key.F:
            case Key.F11:
                _vm.ToggleFullscreenCommand.Execute(null);
                break;
            case Key.M:
                _vm.ToggleMuteCommand.Execute(null);
                break;
            case Key.Escape:
                if (_vm.IsFullscreen) _vm.ToggleFullscreenCommand.Execute(null);
                else                  _vm.BackCommand.Execute(null);
                break;
            case Key.Up:
                _vm.Volume = Math.Min(100, _vm.Volume + 5);
                break;
            case Key.Down:
                _vm.Volume = Math.Max(0, _vm.Volume - 5);
                break;
        }

        ShowControls();
    }

    // ── Controls auto-hide ────────────────────────────────────────────────────
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        ShowControls();
    }

    private void ControlsOverlay_MouseEnter(object sender, MouseEventArgs e)
    {
        _controlsLocked = true;
        ShowControls();
        _hideTimer.Stop();
    }

    private void ControlsOverlay_MouseLeave(object sender, MouseEventArgs e)
    {
        _controlsLocked = false;
        _hideTimer.Start();
    }

    private void ShowControls()
    {
        ControlsOverlay.Visibility = Visibility.Visible;
        Cursor = Cursors.Arrow;
        _hideTimer.Stop();
        if (!_controlsLocked) _hideTimer.Start();
    }

    private void HideControls()
    {
        if (_controlsLocked) return;
        ControlsOverlay.Visibility = Visibility.Collapsed;
        // Hide cursor in fullscreen only
        if (_vm.IsFullscreen) Cursor = Cursors.None;
        _hideTimer.Stop();
    }
}
