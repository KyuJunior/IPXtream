using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using IPXtream.ViewModels;
using FlyleafLib;

namespace IPXtream.Views;

public partial class DashboardWindow : Window
{
    private readonly DashboardViewModel _vm;

    // Auto-hide controls timer
    private readonly System.Windows.Threading.DispatcherTimer _hideTimer;
    private bool _barsLocked;
    private bool _isFullscreen;

    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        Engine.Start(new EngineConfig()
        {
            FFmpegPath = ":FFmpeg",
            LogOutput  = ":debug"
        });

        _vm.PlayRequested   += OnPlayRequested;
        _vm.LogoutRequested += OnLogoutRequested;

        // Force popup position update on resize/move since WPF popups don't track inherently
        this.LocationChanged += (_, _) => UpdatePopupPosition();
        this.SizeChanged     += (_, _) => UpdatePopupPosition();

        _hideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hideTimer.Tick += (_, _) => HideBars();
    }

    // ── Play: show embedded player ────────────────────────────────────────────
    private void OnPlayRequested(Models.StreamItem stream)
    {
        // Clean up any previous session
        if (_vm.PlayerVm is not null)
        {
            _vm.PlayerVm.Dispose();
            _vm.PlayerVm = null;
        }

        // Build new PlayerViewModel and attach to the dashboard VM
        var playerVm = new PlayerViewModel(App.ApiService, stream, _vm);
        _vm.PlayerVm = playerVm;

        // Build stream URL
        var c = App.CurrentCredentials!;
        var s = stream;
        var url = s.StreamType switch
        {
            "movie"  => $"{c.BaseUrl}/movie/{c.Username}/{c.Password}/{s.EffectiveStreamId}.{s.ContainerExtension}",
            "series" => $"{c.BaseUrl}/series/{c.Username}/{c.Password}/{s.EffectiveStreamId}.{s.ContainerExtension}",
            _        => $"{c.BaseUrl}/live/{c.Username}/{c.Password}/{s.EffectiveStreamId}.{s.ContainerExtension}"
        };

        // IMPORTANT: Show the player panel and update layout BEFORE opening stream
        // Otherwise FlyleafHost has 0x0 size and falls back to a standalone popout window.
        PlayerPanel.Visibility = Visibility.Visible;
        PlayerPanel.UpdateLayout();

        VideoView.Player = playerVm.Player;
        playerVm.Initialise(url);

        // Wire Close → back to library  
        playerVm.CloseRequested += ClosePlayer;

        // Wire fullscreen property changes
        playerVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
                ApplyFullscreen(playerVm.IsFullscreen);
        };

        PlayerOverlayPopup.IsOpen = true;
        _hideTimer.Start();
    }

    private void ClosePlayer()
    {
        _hideTimer.Stop();

        // Exit fullscreen if active
        if (_isFullscreen) ApplyFullscreen(false);

        // Dispose VM on background thread (avoids LibVLC deadlock)
        var old = _vm.PlayerVm;
        _vm.PlayerVm = null;
        VideoView.Player = null; // Detach FlyleafHost so it doesn't hold reference
        old?.Dispose();

        PlayerOverlayPopup.IsOpen = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
    }

    private void BackToLibrary_Click(object sender, RoutedEventArgs e)
        => ClosePlayer();

    // ── Fullscreen ────────────────────────────────────────────────────────────
    private void ApplyFullscreen(bool go)
    {
        _isFullscreen = go;
        if (go)
        {
            // Set pure black background to hide WPF airspace borders
            Background = System.Windows.Media.Brushes.Black;
            
            WindowStyle = WindowStyle.None;
            // Intentionally keeping ResizeMode.CanResize — setting NoResize 
            // causes a known WPF bug on Win11 where it leaves white margins.
            
            // Force layout refresh
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            
            Topmost = true; // Cover taskbar

            // Extend player panel over the sidebar
            PlayerPanel.Margin = new Thickness(0);
        }
        else
        {
            Topmost = false;
            // Restore original dark dashboard background
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#0F0F1A")!;
            
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode  = ResizeMode.CanResize;
            
            // Restore sidebar gap
            PlayerPanel.Margin = new Thickness(220, 0, 0, 0);
        }
    }

    private void UpdatePopupPosition()
    {
        if (PlayerOverlayPopup.IsOpen)
        {
            // Toggle placement explicitly forces WPF rendering layout recalculation
            var offset = PlayerOverlayPopup.HorizontalOffset;
            PlayerOverlayPopup.HorizontalOffset = offset + 0.1;
            PlayerOverlayPopup.HorizontalOffset = offset;
        }
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.PlayerVm is null) return;

        switch (e.Key)
        {
            case Key.Space:
                _vm.PlayerVm.TogglePlayCommand.Execute(null);
                break;
            case Key.F:
            case Key.F11:
                _vm.PlayerVm.ToggleFullscreenCommand.Execute(null);
                break;
            case Key.M:
                _vm.PlayerVm.ToggleMuteCommand.Execute(null);
                break;
            case Key.Escape:
                if (_isFullscreen) ApplyFullscreen(false);
                else               ClosePlayer();
                break;
            case Key.Up:
                _vm.PlayerVm.Volume = Math.Min(100, _vm.PlayerVm.Volume + 5);
                break;
            case Key.Down:
                _vm.PlayerVm.Volume = Math.Max(0,   _vm.PlayerVm.Volume - 5);
                break;
        }

        ShowBars();
    }

    // ── Mouse events for auto-hide ────────────────────────────────────────────
    private void Player_MouseMove(object sender, MouseEventArgs e) => ShowBars();

    private void Player_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && _vm.PlayerVm is not null)
            _vm.PlayerVm.ToggleFullscreenCommand.Execute(null);
    }

    private void PlayerBar_MouseEnter(object sender, MouseEventArgs e)
    {
        _barsLocked = true;
        ShowBars();
        _hideTimer.Stop();
    }

    private void PlayerBar_MouseLeave(object sender, MouseEventArgs e)
    {
        _barsLocked = false;
        _hideTimer.Start();
    }

    private void ShowBars()
    {
        PlayerTopBar.Opacity          = 1;
        PlayerTopBar.IsHitTestVisible = true;
        PlayerBottomBar.Opacity          = 1;
        PlayerBottomBar.IsHitTestVisible = true;
        Cursor = Cursors.Arrow;
        _hideTimer.Stop();
        if (!_barsLocked) _hideTimer.Start();
    }

    private void HideBars()
    {
        if (_barsLocked) return;
        PlayerTopBar.Opacity          = 0;
        PlayerTopBar.IsHitTestVisible = false;
        PlayerBottomBar.Opacity          = 0;
        PlayerBottomBar.IsHitTestVisible = false;
        if (_isFullscreen) Cursor = Cursors.None;
        _hideTimer.Stop();
    }

    // ── Seekbar drag ──────────────────────────────────────────────────────────
    private void SeekSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (_vm.PlayerVm is not null) _vm.PlayerVm.IsUserSeeking = true;
    }

    private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_vm.PlayerVm is null) return;
        _vm.PlayerVm.IsUserSeeking = false;
        if (sender is System.Windows.Controls.Slider sl)
            _vm.PlayerVm.CommitSeek((float)sl.Value);
    }

    // ── Navigation ────────────────────────────────────────────────────────────
    private void OnLogoutRequested()
    {
        ClosePlayer();
        var login = new LoginWindow(new LoginViewModel(App.ApiService));
        login.Show();
        Close();
    }
}
