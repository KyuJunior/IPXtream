using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using IPXtream.ViewModels;
using FlyleafLib;

namespace IPXtream.Views;

public partial class DashboardWindow : Window
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private readonly DashboardViewModel _vm;

    // Auto-hide controls timer
    private readonly System.Windows.Threading.DispatcherTimer _hideTimer;
    private bool _barsLocked;
    private bool _isFullscreen;

    // VLC and WebView2 fields
    private LibVLCSharp.Shared.LibVLC? _libVLC;
    private LibVLCSharp.Shared.MediaPlayer? _vlcMediaPlayer;
    private TimeSpan _webViewCurrentPosition = TimeSpan.Zero;
    private TimeSpan _webViewDuration = TimeSpan.Zero;

    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // Apply saved theme immediately
        Helpers.ThemeHelper.ApplyTheme(_vm.SelectedTheme);

        // Register DWM backdrop and theme updates
        this.Loaded += (s, e) =>
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int backdropType = 3; // 3 = Acrylic blur backdrop
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                    UpdateTitleBarTheme(_vm.SelectedTheme);
                }
            }
            catch (Exception ex)
            {
                Services.LogService.Log("Failed to enable Windows 11 Acrylic backdrop", ex);
            }
        };

        _vm.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.SelectedTheme))
            {
                UpdateTitleBarTheme(_vm.SelectedTheme);
            }
        };

        // Initialize LibVLC Core safely
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
        }
        catch (Exception ex)
        {
            Services.LogService.Log("Failed to initialize LibVLC Core", ex);
        }

        // Start Flyleaf Engine safely
        if (!Engine.IsLoaded)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDir = System.IO.Path.Combine(localAppData, "IPXtream");
                System.IO.Directory.CreateDirectory(logDir);
                string playerLogPath = System.IO.Path.Combine(logDir, "player.log");

                Services.LogService.Log($"Starting Flyleaf Engine. App Version: {typeof(DashboardWindow).Assembly.GetName().Version}");

                var config = new EngineConfig()
                {
                    FFmpegPath     = ":FFmpeg",
                    LogOutput      = playerLogPath,
                    LogLevel       = FlyleafLib.LogLevel.Debug
                };

                Engine.Start(config);
            }
            catch (Exception ex)
            {
                Services.LogService.Log("Failed to start Flyleaf Engine", ex);
                MessageBox.Show(
                    "Flyleaf Player failed to initialize.\n\n" +
                    "Please ensure that the visual C++ runtimes are installed, and FFmpeg DLLs exist in the FFmpeg directory.\n\n" +
                    "Details: " + ex.Message,
                    "Player Engine Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        _vm.PlayRequested   += OnPlayRequested;
        _vm.LogoutRequested += OnLogoutRequested;
        _vm.RequestGithubToken += OnRequestGithubToken;

        // Force popup position update on resize/move since WPF popups don't track inherently
        this.LocationChanged += (_, _) => UpdatePopupPosition();
        this.SizeChanged     += (_, _) => UpdatePopupPosition();

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

    private void UpdateTitleBarTheme(string themeName)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int darkMode = themeName == "Light Ocean" ? 0 : 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        catch (Exception ex)
        {
            Services.LogService.Log("Failed to update DWM immersive dark mode titlebar", ex);
        }
    }

    // ── Play: show embedded player ────────────────────────────────────────────
    private async void OnPlayRequested(Models.StreamItem stream, System.Collections.Generic.List<Models.StreamItem> siblings)
    {
        // Clean up any previous session
        if (_vm.PlayerVm is not null)
        {
            ClosePlayer();
        }

        string engine = _vm.SelectedPlayerEngine;

        // Build new PlayerViewModel and attach to the dashboard VM
        var playerVm = new PlayerViewModel(App.ApiService, stream, siblings, _vm, engine);
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

        // If MPV is selected, we launch it externally and don't open the panel
        if (engine == "MPV")
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mpv",
                    Arguments = $"\"{url}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not launch external MPV player.\n\n" +
                    "Please verify that MPV is installed on your system and added to your environmental PATH variable.\n\n" +
                    "Details: " + ex.Message,
                    "MPV Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            // Clear Player VM since we are not using the internal player panel
            _vm.PlayerVm = null;
            return;
        }

        // IMPORTANT: Show the player panel and update layout BEFORE opening stream
        PlayerPanel.Visibility = Visibility.Visible;
        PlayerPanel.UpdateLayout();

        // Toggle player controls visibilities
        VideoView.Visibility = (engine == "Flyleaf") ? Visibility.Visible : Visibility.Collapsed;
        VlcVideoView.Visibility = (engine == "VLC") ? Visibility.Visible : Visibility.Collapsed;
        PlayerMediaElement.Visibility = (engine == "MediaElement") ? Visibility.Visible : Visibility.Collapsed;
        PlayerWebView.Visibility = (engine == "WebView2") ? Visibility.Visible : Visibility.Collapsed;

        // Configure the selected engine
        if (engine == "Flyleaf")
        {
            VideoView.Player = playerVm.Player;
        }
        else if (engine == "VLC")
        {
            try
            {
                _libVLC ??= new LibVLCSharp.Shared.LibVLC();
                _vlcMediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
                VlcVideoView.MediaPlayer = _vlcMediaPlayer;

                // Wire actions
                playerVm.PlayAction = () => _vlcMediaPlayer.Play();
                playerVm.PauseAction = () => _vlcMediaPlayer.Pause();
                playerVm.StopAction = () => _vlcMediaPlayer.Stop();
                playerVm.SeekAction = (time) => _vlcMediaPlayer.Time = (long)time.TotalMilliseconds;
                playerVm.SetVolumeAction = (vol) => _vlcMediaPlayer.Volume = (int)vol;
                playerVm.SetMuteAction = (mute) => _vlcMediaPlayer.Mute = mute;
                playerVm.SetSpeedAction = (speed) => _vlcMediaPlayer.SetRate((float)speed);
                playerVm.OpenUrlAction = (u) => 
                {
                    using var media = new LibVLCSharp.Shared.Media(_libVLC, new Uri(u));
                    _vlcMediaPlayer.Play(media);
                };

                playerVm.GetCurrentPositionFunc = () => TimeSpan.FromMilliseconds(_vlcMediaPlayer.Time);
                playerVm.GetDurationFunc = () => TimeSpan.FromMilliseconds(_vlcMediaPlayer.Length);

                // Wire events
                _vlcMediaPlayer.Playing += VlcMediaPlayer_Playing;
                _vlcMediaPlayer.Paused += VlcMediaPlayer_Paused;
                _vlcMediaPlayer.EncounteredError += VlcMediaPlayer_Error;
                _vlcMediaPlayer.EndReached += VlcMediaPlayer_EndReached;
                _vlcMediaPlayer.Buffering += VlcMediaPlayer_Buffering;
            }
            catch (Exception ex)
            {
                Services.LogService.Log("Failed to initialize VLC Player", ex);
                MessageBox.Show("Failed to initialize VLC Player components.\n\n" + ex.Message, "VLC Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ClosePlayer();
                return;
            }
        }
        else if (engine == "MediaElement")
        {
            // Wire actions
            playerVm.PlayAction = () => PlayerMediaElement.Play();
            playerVm.PauseAction = () => PlayerMediaElement.Pause();
            playerVm.StopAction = () => PlayerMediaElement.Stop();
            playerVm.SeekAction = (time) => PlayerMediaElement.Position = time;
            playerVm.SetVolumeAction = (vol) => PlayerMediaElement.Volume = vol / 100.0;
            playerVm.SetMuteAction = (mute) => PlayerMediaElement.IsMuted = mute;
            playerVm.SetSpeedAction = (speed) => PlayerMediaElement.SpeedRatio = speed;
            playerVm.OpenUrlAction = (u) =>
            {
                PlayerMediaElement.Source = new Uri(url); // Wait, use url here to resolve local vs network
                PlayerMediaElement.Play();
            };

            playerVm.GetCurrentPositionFunc = () => PlayerMediaElement.Position;
            playerVm.GetDurationFunc = () => PlayerMediaElement.NaturalDuration.HasTimeSpan ? PlayerMediaElement.NaturalDuration.TimeSpan : TimeSpan.Zero;

            // Wire events
            PlayerMediaElement.MediaOpened += MediaElement_MediaOpened;
            PlayerMediaElement.MediaFailed += MediaElement_MediaFailed;
            PlayerMediaElement.MediaEnded += MediaElement_MediaEnded;
        }
        else if (engine == "WebView2")
        {
            try
            {
                _webViewCurrentPosition = TimeSpan.Zero;
                _webViewDuration = TimeSpan.Zero;

                await PlayerWebView.EnsureCoreWebView2Async();

                // Hook messages
                PlayerWebView.WebMessageReceived += WebView_WebMessageReceived;

                string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body { margin: 0; background: black; overflow: hidden; height: 100vh; display: flex; justify-content: center; align-items: center; }
        video { width: 100%; height: 100%; outline: none; }
    </style>
    <script src='https://cdn.jsdelivr.net/npm/hls.js@1.4.12/dist/hls.min.js'></script>
</head>
<body>
    <video id='videoPlayer' autoplay controls></video>
    <script>
        const video = document.getElementById('videoPlayer');
        let hlsInstance = null;
        
        window.play = function(url) {
            if (hlsInstance) {
                hlsInstance.destroy();
                hlsInstance = null;
            }
            if (url.includes('.m3u8')) {
                if (Hls.isSupported()) {
                    hlsInstance = new Hls();
                    hlsInstance.loadSource(url);
                    hlsInstance.attachMedia(video);
                    hlsInstance.on(Hls.Events.MANIFEST_PARSED, function() {
                        video.play();
                    });
                } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
                    video.src = url;
                }
            } else {
                video.src = url;
            }
        };

        video.onplaying = () => window.chrome.webview.postMessage('playing');
        video.onpause = () => window.chrome.webview.postMessage('paused');
        video.onended = () => window.chrome.webview.postMessage('ended');
        video.onerror = () => window.chrome.webview.postMessage('error:' + (video.error ? video.error.message : 'Playback failed'));
        video.ontimeupdate = () => {
            window.chrome.webview.postMessage('timeupdate:' + video.currentTime + ',' + (video.duration || 0));
        };
    </script>
</body>
</html>";
                PlayerWebView.NavigateToString(html);

                // Wire actions
                playerVm.PlayAction = () => PlayerWebView.ExecuteScriptAsync("document.getElementById('videoPlayer').play();");
                playerVm.PauseAction = () => PlayerWebView.ExecuteScriptAsync("document.getElementById('videoPlayer').pause();");
                playerVm.StopAction = () => PlayerWebView.ExecuteScriptAsync("const v = document.getElementById('videoPlayer'); v.pause(); v.src = '';");
                playerVm.SeekAction = (time) => PlayerWebView.ExecuteScriptAsync($"document.getElementById('videoPlayer').currentTime = {time.TotalSeconds};");
                playerVm.SetVolumeAction = (vol) => PlayerWebView.ExecuteScriptAsync($"document.getElementById('videoPlayer').volume = {vol / 100.0};");
                playerVm.SetMuteAction = (mute) => PlayerWebView.ExecuteScriptAsync($"document.getElementById('videoPlayer').muted = {mute.ToString().ToLower()};");
                playerVm.SetSpeedAction = (speed) => PlayerWebView.ExecuteScriptAsync($"document.getElementById('videoPlayer').playbackRate = {speed};");
                playerVm.OpenUrlAction = (u) => PlayerWebView.ExecuteScriptAsync($"window.play('{u}');");

                playerVm.GetCurrentPositionFunc = () => _webViewCurrentPosition;
                playerVm.GetDurationFunc = () => _webViewDuration;
            }
            catch (Exception ex)
            {
                Services.LogService.Log("Failed to initialize WebView2 Player", ex);
                MessageBox.Show("Failed to initialize WebView2 components.\n\n" + ex.Message, "WebView2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ClosePlayer();
                return;
            }
        }

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

        // Unsubscribe VLC events
        if (_vlcMediaPlayer != null)
        {
            try
            {
                _vlcMediaPlayer.Playing -= VlcMediaPlayer_Playing;
                _vlcMediaPlayer.Paused -= VlcMediaPlayer_Paused;
                _vlcMediaPlayer.EncounteredError -= VlcMediaPlayer_Error;
                _vlcMediaPlayer.EndReached -= VlcMediaPlayer_EndReached;
                _vlcMediaPlayer.Buffering -= VlcMediaPlayer_Buffering;
                _vlcMediaPlayer.Stop();
                _vlcMediaPlayer.Dispose();
            }
            catch {}
            _vlcMediaPlayer = null;
        }

        // Unsubscribe MediaElement events
        PlayerMediaElement.MediaOpened -= MediaElement_MediaOpened;
        PlayerMediaElement.MediaFailed -= MediaElement_MediaFailed;
        PlayerMediaElement.MediaEnded -= MediaElement_MediaEnded;
        PlayerMediaElement.Stop();
        PlayerMediaElement.Source = null;

        // Unsubscribe WebView2 events
        PlayerWebView.WebMessageReceived -= WebView_WebMessageReceived;
        if (PlayerWebView.CoreWebView2 != null)
        {
            try
            {
                PlayerWebView.CoreWebView2.Navigate("about:blank");
            }
            catch {}
        }

        // Dispose VM on background thread (avoids UI thread locks/deadlocks)
        var old = _vm.PlayerVm;
        _vm.PlayerVm = null;
        VideoView.Player = null; // Detach FlyleafHost so it doesn't hold reference
        
        System.Threading.Tasks.Task.Run(() =>
        {
            old?.Dispose();
        });

        PlayerOverlayPopup.IsOpen = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
    }

    // ── VLC Event Handlers ───────────────────────────────────────────────────
    private void VlcMediaPlayer_Playing(object? sender, EventArgs e) => _vm.PlayerVm?.OnMediaOpened();
    private void VlcMediaPlayer_Paused(object? sender, EventArgs e) { }
    private void VlcMediaPlayer_Error(object? sender, EventArgs e) => _vm.PlayerVm?.OnMediaFailed("VLC playback error.");
    private void VlcMediaPlayer_EndReached(object? sender, EventArgs e) => _vm.PlayerVm?.OnMediaEnded();
    private void VlcMediaPlayer_Buffering(object? sender, LibVLCSharp.Shared.MediaPlayerBufferingEventArgs e)
    {
        if (e.Cache == 100)
            _vm.PlayerVm?.OnBufferingEnded();
        else
            _vm.PlayerVm?.OnBufferingStarted();
    }

    // ── MediaElement Event Handlers ──────────────────────────────────────────
    private void MediaElement_MediaOpened(object sender, RoutedEventArgs e) => _vm.PlayerVm?.OnMediaOpened();
    private void MediaElement_MediaFailed(object? sender, ExceptionRoutedEventArgs e) => _vm.PlayerVm?.OnMediaFailed(e.ErrorException?.Message ?? "Native playback failed.");
    private void MediaElement_MediaEnded(object sender, RoutedEventArgs e) => _vm.PlayerVm?.OnMediaEnded();

    // ── WebView2 Event Handlers ──────────────────────────────────────────────
    private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg = e.TryGetWebMessageAsString();
        if (msg == "playing")
        {
            _vm.PlayerVm?.OnMediaOpened();
        }
        else if (msg == "ended")
        {
            _vm.PlayerVm?.OnMediaEnded();
        }
        else if (msg.StartsWith("error:"))
        {
            _vm.PlayerVm?.OnMediaFailed(msg.Substring(6));
        }
        else if (msg.StartsWith("timeupdate:"))
        {
            var parts = msg.Substring(11).Split(',');
            if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double curSec) && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double durSec))
            {
                _webViewCurrentPosition = TimeSpan.FromSeconds(curSec);
                _webViewDuration = TimeSpan.FromSeconds(durSec);
            }
        }
    }

    private void BackToLibrary_Click(object sender, RoutedEventArgs e)
        => ClosePlayer();

    // ── Fullscreen ────────────────────────────────────────────────────────────
    private void ApplyFullscreen(bool go)
    {
        _isFullscreen = go;

        // Temporarily close popup to trigger HWND recreation on top of the fullscreen topmost window
        PlayerOverlayPopup.IsOpen = false;

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

        // Re-open popup so it is created on top of the active topmost window with the correct size
        PlayerOverlayPopup.IsOpen = true;
        UpdatePopupPosition();
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

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _vm.IsSettingsOpen)
        {
            _vm.CloseSettingsCommand.Execute(null);
            e.Handled = true;
            return;
        }

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
            case Key.N:
                if (_vm.PlayerVm.PlayNextEpisodeCommand.CanExecute(null))
                    _vm.PlayerVm.PlayNextEpisodeCommand.Execute(null);
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
        App.BypassAutoLogin = true;
        var login = new LoginWindow(new LoginViewModel(App.ApiService));
        login.Show();
        Close();
    }

    // ── Secret Admin Verification & Push Token Prompt ──────────────────────────
    private int _versionClickCount = 0;
    private DateTime _lastVersionClickTime = DateTime.MinValue;

    private void Version_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        if ((now - _lastVersionClickTime).TotalMilliseconds > 1500)
        {
            _versionClickCount = 1;
        }
        else
        {
            _versionClickCount++;
        }
        _lastVersionClickTime = now;

        if (_versionClickCount >= 5)
        {
            _versionClickCount = 0;
            
            var passwordBox = new System.Windows.Controls.PasswordBox
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C1C28")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33FFFFFF")),
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var btnVerify = new System.Windows.Controls.Button
            {
                Content = "Verify",
                Height = 32,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4F8EF7")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true
            };

            var dialog = new Window
            {
                Title = "Developer Verification",
                Width = 320,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#121212")),
                Foreground = System.Windows.Media.Brushes.White,
                Content = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new System.Windows.Controls.TextBlock { Text = "Enter Admin Password:", Margin = new Thickness(0, 0, 0, 8), Foreground = System.Windows.Media.Brushes.White, FontSize = 12 },
                        passwordBox,
                        btnVerify
                    }
                }
            };

            btnVerify.Click += (s, args) =>
            {
                if (passwordBox.Password == "ipxtreamadmin2026")
                {
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("Incorrect Password", "Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            if (dialog.ShowDialog() == true)
            {
                _vm.ToggleAdminMode();
                MessageBox.Show(_vm.IsAdminMode ? "Admin Mode Enabled" : "Admin Mode Disabled", "Developer Status", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void OnRequestGithubToken(TaskCompletionSource<string?> tcs)
    {
        var textBox = new System.Windows.Controls.TextBox
        {
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C1C28")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33FFFFFF")),
            Height = 28,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        };

        var btnSubmit = new System.Windows.Controls.Button
        {
            Content = "Save & Push",
            Height = 32,
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4F8EF7")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            IsDefault = true
        };

        var dialog = new Window
        {
            Title = "GitHub Verification",
            Width = 360,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#121212")),
            Foreground = System.Windows.Media.Brushes.White,
            Content = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new System.Windows.Controls.TextBlock 
                    { 
                        Text = "Enter GitHub Personal Access Token (with repo write):", 
                        Margin = new Thickness(0, 0, 0, 8), 
                        Foreground = System.Windows.Media.Brushes.White, 
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap
                    },
                    textBox,
                    btnSubmit
                }
            }
        };

        btnSubmit.Click += (s, args) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };

        if (dialog.ShowDialog() == true)
        {
            tcs.SetResult(textBox.Text.Trim());
        }
        else
        {
            tcs.SetResult(null);
        }
    }

    private void SettingsOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            _vm.CloseSettingsCommand.Execute(null);
        }
    }
}
