using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using IPXtream.ViewModels;

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

        _vm.PlayRequested   += OnPlayRequested;
        _vm.LogoutRequested += OnLogoutRequested;

        _hideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hideTimer.Tick += (_, _) => HideBars();

        // Global input hook to track MouseMove
        InputManager.Current.PreProcessInput += (s, e) =>
        {
            if (e.StagingItem.Input.RoutedEvent == Mouse.MouseMoveEvent && _vm?.PlayerVm is not null)
            {
                ShowBars();
            }
        };
    }

    // ── Play: show embedded player ────────────────────────────────────────────
    private void OnPlayRequested(Models.StreamItem stream)
    {
        // Clean up any previous session
        if (_vm.PlayerVm is not null)
        {
            ClosePlayer();
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

        // If the media file has been downloaded locally, play the local path instead
        var downloadDir = string.Empty;
        if (DataContext is DashboardViewModel vm)
        {
            downloadDir = vm.DownloadFolder;
        }
        else
        {
            downloadDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "IPXtream");
        }
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safeName = string.Concat(s.Name.Select(ch => Array.IndexOf(invalid, ch) >= 0 ? '_' : ch))
                             .Trim().TrimEnd('.');
        var localPath = System.IO.Path.Combine(downloadDir, $"{safeName}.{s.ContainerExtension}");

        if (System.IO.File.Exists(localPath))
        {
            url = localPath;
        }

        // Show the player panel and update layout
        PlayerPanel.Visibility = Visibility.Visible;
        PlayerPanel.UpdateLayout();

        // Wire VM actions/funcs to native MediaElement
        playerVm.PlayAction = () => PlayerMediaElement.Play();
        playerVm.PauseAction = () => PlayerMediaElement.Pause();
        playerVm.StopAction = () => PlayerMediaElement.Stop();
        playerVm.SeekAction = (time) => PlayerMediaElement.Position = time;
        playerVm.SetVolumeAction = (vol) => PlayerMediaElement.Volume = vol;
        playerVm.SetMuteAction = (mute) => PlayerMediaElement.IsMuted = mute;
        playerVm.SetSpeedAction = (speed) => PlayerMediaElement.SpeedRatio = speed;
        playerVm.OpenUrlAction = (u) =>
        {
            PlayerMediaElement.Source = new Uri(u, UriKind.RelativeOrAbsolute);
            PlayerMediaElement.Play();
        };

        playerVm.GetCurrentPositionFunc = () => PlayerMediaElement.Position;
        playerVm.GetDurationFunc = () => PlayerMediaElement.NaturalDuration.HasTimeSpan ? PlayerMediaElement.NaturalDuration.TimeSpan : TimeSpan.Zero;

        // Initialize state on MediaElement
        PlayerMediaElement.Volume = playerVm.Volume / 100.0;
        PlayerMediaElement.SpeedRatio = playerVm.SelectedSpeed;
        PlayerMediaElement.IsMuted = playerVm.IsMuted;

        // Subscribe to events
        PlayerMediaElement.MediaOpened += OnMediaOpened;
        PlayerMediaElement.MediaFailed += OnMediaFailed;
        PlayerMediaElement.MediaEnded += OnMediaEnded;

        playerVm.Initialise(url);

        // Wire Close → back to library  
        playerVm.CloseRequested += ClosePlayer;

        // Wire fullscreen property changes
        playerVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
                ApplyFullscreen(playerVm.IsFullscreen);
        };

        _hideTimer.Start();
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        _vm.PlayerVm?.OnMediaOpened();
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        _vm.PlayerVm?.OnMediaFailed(e.ErrorException?.Message ?? "Media playback failed");
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _vm.PlayerVm?.OnMediaEnded();
    }

    private void ClosePlayer()
    {
        _hideTimer.Stop();

        // Exit fullscreen if active
        if (_isFullscreen) ApplyFullscreen(false);

        // Unsubscribe events
        PlayerMediaElement.MediaOpened -= OnMediaOpened;
        PlayerMediaElement.MediaFailed -= OnMediaFailed;
        PlayerMediaElement.MediaEnded -= OnMediaEnded;

        // Stop and clear MediaElement source
        PlayerMediaElement.Stop();
        PlayerMediaElement.Source = null;

        // Dispose VM
        var old = _vm.PlayerVm;
        _vm.PlayerVm = null;
        old?.Dispose();

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
            // Set pure black background
            Background = System.Windows.Media.Brushes.Black;
            
            WindowStyle = WindowStyle.None;
            
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
