using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPXtream.Models;
using IPXtream.Services;

namespace IPXtream.ViewModels;

/// <summary>
/// Sidebar section identifiers.
/// </summary>
public enum MediaSection { LiveTV, VOD, Series, WhatsNew, CurrentlyWatching, Downloads, Settings, Home }

/// <summary>
/// ViewModel for DashboardWindow.
/// Drives the sidebar, category list, stream list, and search.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly XtreamApiService _api;
    private DispatcherTimer? _searchTimer;
    private CancellationTokenSource? _categoryLoadingCts;
    private CancellationTokenSource? _cacheCts;
    private SemaphoreSlim _downloadSemaphore = new(2); // max concurrent downloads
    private readonly List<StreamItem> _featuredItems = new();
    private readonly HashSet<string> _featuredKeys = new();
    private int _currentSeriesId;

    // ── Auth info (displayed in header) ───────────────────────────────────────
    [ObservableProperty] private string _displayUsername = string.Empty;
    [ObservableProperty] private string _serverDisplay = string.Empty;

    // ── Version & Update Check ────────────────────────────────────────────────
    public string AppVersion { get; } =
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.5.2"}";

    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private bool   _isCheckingUpdate;
    [ObservableProperty] private string _updateUrl = string.Empty;
    [ObservableProperty] private bool   _isDownloadingUpdate;
    [ObservableProperty] private double _updateDownloadProgress;
    [ObservableProperty] private string _updateDownloadStatus = string.Empty;
    private string _updateDownloadUrl = string.Empty;

    [ObservableProperty] private bool _isAdminMode;

    [ObservableProperty] private bool _isSwitchAccountOpen;
    [ObservableProperty] private bool _isSwitchingAccount;
    [ObservableProperty] private string _switchingAccountStatus = string.Empty;
    [ObservableProperty] private string _switchingAccountError = string.Empty;

    // ── Sidebar selection ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeroBanner))]
    private MediaSection _activeSection = MediaSection.Home;

    // ── Carousel (for What's New Hero) ────────────────────────────────────────
    public ObservableCollection<StreamItem> FeaturedItems { get; } = new();
    public ObservableCollection<StreamItem> FeaturedLive { get; } = new();
    public ObservableCollection<StreamItem> FeaturedShows { get; } = new();
    public ObservableCollection<StreamItem> FeaturedMovies { get; } = new();
    public ObservableCollection<StreamItem> DevRecommendations { get; } = new();
    public ObservableCollection<StreamItem> RecentlyWatched { get; } = new();

    // ── My Library Sections ──────────────────────────────────────────────────
    public ObservableCollection<StreamItem> ContinueWatching { get; } = new();
    public ObservableCollection<StreamItem> LikedShows { get; } = new();
    public ObservableCollection<StreamItem> LikedLive { get; } = new();
    public ObservableCollection<StreamItem> LikedMovies { get; } = new();
    public ObservableCollection<StreamItem> WatchLater { get; } = new();

    [ObservableProperty]
    private StreamItem? _selectedSeriesForInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeroBanner))]
    private StreamItem? _featuredCarouselItem;

    public bool ShowHeroBanner => ActiveSection == MediaSection.WhatsNew && CurrentSeries == null && FeaturedCarouselItem != null;

    partial void OnFeaturedCarouselItemChanged(StreamItem? oldValue, StreamItem? newValue)
    {
        if (oldValue != null) oldValue.IsSelectedCarousel = false;
        if (newValue != null) newValue.IsSelectedCarousel = true;
    }

    // ── Categories ────────────────────────────────────────────────────────────
    public ObservableCollection<Category> Categories { get; } = new();
    private readonly object _categoriesLock = new object();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCategory))]
    private Category? _selectedCategory;

    public bool HasSelectedCategory => SelectedCategory is not null;

    // Auto-load streams when a category is selected via ListBox binding
    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value is null) return;

        try
        {
            _categoryLoadingCts?.Cancel();
            _categoryLoadingCts?.Dispose();
        }
        catch (Exception ex)
        {
            LogService.Log("Error cancelling previous stream load.", ex);
        }

        _categoryLoadingCts = new CancellationTokenSource();
        _ = LoadStreamsAsync(value.CategoryId, forceRefresh: false, ct: _categoryLoadingCts.Token);
    }


    public ObservableCollection<StreamItem> Streams { get; } = new();
    private readonly object _streamsLock = new object();

    [ObservableProperty]
    private StreamItem? _selectedStream;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewingMovieInfo))]
    private StreamItem? _selectedMovieForInfo;

    public bool IsViewingMovieInfo => SelectedMovieForInfo is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewingSeriesInfo))]
    [NotifyPropertyChangedFor(nameof(ShowHeroBanner))]
    private SeriesInfoResponse? _currentSeries;

    public bool IsViewingSeriesInfo => CurrentSeries is not null;

    // ── Seasons (for series details view) ─────────────────────────────────────
    public ObservableCollection<SeasonItem> Seasons { get; } = new();
    
    [ObservableProperty]
    private SeasonItem? _selectedSeason;

    partial void OnSelectedSeasonChanged(SeasonItem? value)
    {
        Streams.Clear();
        _allStreams.Clear();
        
        if (value is null)
        {
            LogService.Log("OnSelectedSeasonChanged: value is null");
            StatusMessage = string.Empty;
            return;
        }

        LogService.Log($"OnSelectedSeasonChanged: {value.DisplayName}, EpisodesCount={value.Episodes.Count}");

        var episodesList = new List<StreamItem>();
        foreach (var ep in value.Episodes)
        {
            _ = int.TryParse(ep.Id, out int epId);
            var si = new StreamItem
            {
                Name               = string.IsNullOrWhiteSpace(ep.Title) ? ep.Info.Name : ep.Title,
                StreamType         = "series",
                StreamId           = epId,
                VideoId            = epId,
                SeriesId           = _currentSeriesId,
                ContainerExtension = ep.ContainerExtension ?? "mp4",
                Plot               = ep.Info.Plot,
                CustomSid          = ep.CustomSid,
                StreamIcon         = !string.IsNullOrEmpty(ep.Info.MovieImage) ? ep.Info.MovieImage : (CurrentSeries?.Info.Cover ?? string.Empty),
                SeriesName         = CurrentSeries?.Info?.Name,
                EpisodeTitle       = string.IsNullOrWhiteSpace(ep.Title) ? ep.Info.Name : ep.Title,
                SeasonName         = value.DisplayName
            };
            MarkFeatured(si);
            episodesList.Add(si);
        }
        _allStreams = episodesList;
        SetFilteredStreams(episodesList);

        LogService.Log($"OnSelectedSeasonChanged: Streams.Count={Streams.Count}, _allStreams.Count={_allStreams.Count}, IsViewingSeriesInfo={IsViewingSeriesInfo}");
        StatusMessage = $"{value.Episodes.Count} episodes in {value.DisplayName}";
    }

    // ── Search ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;

    private List<StreamItem> _allStreams = new();  // unfiltered master list

    // ── Pagination ────────────────────────────────────────────────────────────
    public const int PageSize = 32;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(HasNextPage))]
    [NotifyPropertyChangedFor(nameof(PageStatusText))]
    private int _currentPage = 1;

    private List<StreamItem> _filteredStreams = new();

    public int TotalPages => _filteredStreams.Count == 0 ? 1 : (int)Math.Ceiling((double)_filteredStreams.Count / PageSize);

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public string PageStatusText => $"Page {CurrentPage} of {TotalPages} (Total: {_filteredStreams.Count})";

    private void SetFilteredStreams(IEnumerable<StreamItem> items)
    {
        _filteredStreams = items.ToList();
        CurrentPage = 1;
        UpdatePage();
    }

    private void UpdatePage()
    {
        Streams.Clear();
        var pageItems = _filteredStreams
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize);

        foreach (var item in pageItems)
        {
            Streams.Add(item);
        }

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(PageStatusText));
    }

    [RelayCommand]
    private void GoToNextPage()
    {
        if (HasNextPage)
        {
            CurrentPage++;
            UpdatePage();
        }
    }

    [RelayCommand]
    private void GoToPreviousPage()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
            UpdatePage();
        }
    }

    // ── Status / loading ──────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoadingCategories;
    [ObservableProperty] private bool   _isLoadingStreams;
    [ObservableProperty] private bool   _isCachingAll;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage  = string.Empty;

    // ── Event: Open player ────────────────────────────────────────────────────
    public event Action<StreamItem, List<StreamItem>>? PlayRequested;

    // ── Embedded player VM (null when no stream is playing) ───────────────────
    [ObservableProperty]
    private PlayerViewModel? _playerVm;

    // ── PiP (Picture-in-Picture) state ────────────────────────────────────────
    [ObservableProperty]
    private bool _isPlayerMinimized;

    // ── Event: Logout ─────────────────────────────────────────────────────────
    public event Action? LogoutRequested;

    // ── Downloads ─────────────────────────────────────────────────────────────
    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    [ObservableProperty] private bool _showDownloadsTray;
    [ObservableProperty] private string _downloadFolder = string.Empty;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isWhatsNewOpen;
    [ObservableProperty] private bool _isLoadingHomeData;

    [ObservableProperty] private string _newAccountServerUrl = string.Empty;
    [ObservableProperty] private string _newAccountUsername = string.Empty;
    [ObservableProperty] private string _newAccountPassword = string.Empty;
    [ObservableProperty] private string _newAccountErrorMessage = string.Empty;

    public bool HasDownloads        => Downloads.Count > 0;
    public bool HasActiveDownloads  => Downloads.Any(d => d.IsActive);
    public int  ActiveDownloadCount => Downloads.Count(d => d.IsActive);


    // ── Constructor ───────────────────────────────────────────────────────────
    public DashboardViewModel(
        XtreamApiService api,
        UserCredentials creds,
        AuthResponse auth)
    {
        _api            = api;
        DisplayUsername = creds.Username;
        ServerDisplay   = new Uri(creds.BaseUrl).Host;

#if DEBUG
        _isAdminMode = true;
#else
        _isAdminMode = false;
#endif

        LoadSettings();

        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Streams, _streamsLock);
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Categories, _categoriesLock);

        Downloads.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDownloads));
            OnPropertyChanged(nameof(HasActiveDownloads));
            OnPropertyChanged(nameof(ActiveDownloadCount));
        };

        _ = InitializeWhatsNewAsync();

        // silently check for updates on startup
        _ = RunUpdateCheckAsync(silent: true);
    }

    // ── Update check ─────────────────────────────────────────────────────────
    private const string GitHubApiUrl =
        "https://api.github.com/repos/KyuJunior/IPXtream/releases/latest";

    /// <summary>
    /// Parameterless relay command bound to the XAML "Check" button.
    /// A parameterless command generates AsyncRelayCommand (not AsyncRelayCommand&lt;bool&gt;),
    /// so CanExecute works correctly when no CommandParameter is supplied.
    /// </summary>
    [RelayCommand]
    private Task CheckForUpdatesAsync() => RunUpdateCheckAsync(silent: false);

    private async Task RunUpdateCheckAsync(bool silent = false)
    {
        if (IsCheckingUpdate || IsDownloadingUpdate) return;
        IsCheckingUpdate = true;
        UpdateStatus = "Checking…";

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("IPXtream", AppVersion.TrimStart('v')));

            var json = await http.GetStringAsync(GitHubApiUrl);
            using var doc  = JsonDocument.Parse(json);
            var tagName    = doc.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            var htmlUrl    = doc.RootElement.GetProperty("html_url").GetString()  ?? string.Empty;

            // Extract the installer asset download URL
            string downloadUrl = string.Empty;
            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? string.Empty;
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                        break;
                    }
                }
            }
            _updateDownloadUrl = downloadUrl;

            // Normalise: remove leading 'v' for comparison
            var latestStr  = tagName.TrimStart('v');
            var currentStr = AppVersion.TrimStart('v');

            if (Version.TryParse(latestStr,  out var latest) &&
                Version.TryParse(currentStr, out var current))
            {
                if (latest > current)
                {
                    UpdateStatus = $"⬆ Update available: v{latestStr}";
                    UpdateUrl    = htmlUrl;
                }
                else
                {
                    UpdateStatus = "✔ Up to date";
                    UpdateUrl    = string.Empty;
                }
            }
            else
            {
                UpdateStatus = tagName == currentStr ? "✔ Up to date" : $"Latest: {tagName}";
                UpdateUrl    = htmlUrl;
            }
        }
        catch (Exception ex)
        {
            LogService.Log("Update check API failed, trying fallback...", ex);
            
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("IPXtream", AppVersion.TrimStart('v')));

                using var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/KyuJunior/IPXtream/releases/latest");
                using var response = await http.SendAsync(request);
                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;

                if (!string.IsNullOrEmpty(finalUrl) && finalUrl.Contains("/releases/tag/"))
                {
                    var tagIdx = finalUrl.IndexOf("/releases/tag/");
                    var tagName = finalUrl.Substring(tagIdx + "/releases/tag/".Length).Trim();

                    var htmlUrl = finalUrl;
                    var latestStr = tagName.TrimStart('v');
                    var currentStr = AppVersion.TrimStart('v');

                    // Construct download URL
                    string downloadUrl = $"https://github.com/KyuJunior/IPXtream/releases/download/{tagName}/IPXtream_Setup_{tagName}.exe";
                    _updateDownloadUrl = downloadUrl;

                    if (Version.TryParse(latestStr, out var latest) &&
                        Version.TryParse(currentStr, out var current))
                    {
                        if (latest > current)
                        {
                            UpdateStatus = $"⬆ Update available: v{latestStr}";
                            UpdateUrl = htmlUrl;
                        }
                        else
                        {
                            UpdateStatus = "✔ Up to date";
                            UpdateUrl = string.Empty;
                        }
                    }
                    else
                    {
                        UpdateStatus = tagName == currentStr ? "✔ Up to date" : $"Latest: {tagName}";
                        UpdateUrl = htmlUrl;
                    }
                    return; // Succeeded via fallback!
                }
            }
            catch (Exception ex2)
            {
                LogService.Log("Update check fallback failed", ex2);
            }

            UpdateStatus = silent ? string.Empty : "⚠ Could not check";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenUpdatePage()
    {
        if (!string.IsNullOrEmpty(UpdateUrl))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName  = UpdateUrl,
                UseShellExecute = true
            });
    }

    [RelayCommand]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl))
        {
            // Fallback to opening the browser release page
            OpenUpdatePage();
            return;
        }

        if (IsDownloadingUpdate) return;

        // Check if there is already a System Update in the queue/downloads
        if (Downloads.Any(d => d.IsAppUpdate && (d.Status == DownloadStatus.Downloading || d.Status == DownloadStatus.Queued)))
        {
            ActiveSection = MediaSection.Downloads;
            return;
        }

        IsDownloadingUpdate = true;
        UpdateDownloadProgress = 0;
        UpdateDownloadStatus = "Starting download...";

        var destPath = Path.Combine(Path.GetTempPath(), "IPXtream_Installer.exe");

        // If an old update item exists, remove it first
        var old = Downloads.FirstOrDefault(d => d.IsAppUpdate);
        if (old != null)
        {
            Downloads.Remove(old);
        }

        var item = new DownloadItem
        {
            Name        = $"IPXtream Update {UpdateStatus.Replace("⬆ Update available: ", "").Replace("Latest: ", "")}",
            Url         = _updateDownloadUrl,
            Extension   = "exe",
            DestPath    = destPath,
            GroupName   = "System Updates",
            IsAppUpdate = true
        };

        Downloads.Add(item);
        ActiveSection = MediaSection.Downloads;

        _ = RunDownloadTaskAsync(item);
    }

    // ── Sidebar navigation commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task SelectSectionAsync(string section)
    {
        if (section == "settings")
        {
            IsSettingsOpen = true;
            return;
        }

        // If a stream is playing, minimize to PiP instead of blocking navigation
        if (PlayerVm != null)
        {
            IsPlayerMinimized = true;
        }

        ActiveSection     = section switch
        {
            "live"              => MediaSection.LiveTV,
            "vod"               => MediaSection.VOD,
            "series"            => MediaSection.Series,
            "whatsnew"          => MediaSection.WhatsNew,
            "currentlywatching" => MediaSection.CurrentlyWatching,
            "downloads"         => MediaSection.Downloads,
            "home"              => MediaSection.Home,
            _                   => MediaSection.Home
        };

        SelectedCategory = null;
        CurrentSeries    = null;
        SelectedMovieForInfo = null;
        Streams.Clear();
        _allStreams.Clear();
        SearchText = string.Empty;

        if (ActiveSection == MediaSection.WhatsNew)
        {
            LoadRecentlyWatched();
            var featured = _featuredItems.ToList();
            foreach (var item in featured)
            {
                item.IsFeatured = true;
            }
            _allStreams = featured;
            SetFilteredStreams(featured);
            StatusMessage = $"{featured.Count} featured items";
        }
        else if (ActiveSection == MediaSection.Home)
        {
            LoadRecentlyWatched();
            _ = LoadHomeDynamicDataAsync();
            StatusMessage = "Welcome back to IPXtream";
        }
        else if (ActiveSection == MediaSection.CurrentlyWatching)
        {
            LoadLibraryData();
        }
        else if (ActiveSection == MediaSection.Downloads)
        {
            StatusMessage = "Manage your downloads and local folder settings";
        }
        else if (ActiveSection == MediaSection.Settings)
        {
            StatusMessage = "App-wide settings & accounts management";
        }
        else
        {
            await LoadCategoriesAsync();
        }
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void OpenWhatsNew()
    {
        IsWhatsNewOpen = true;
    }

    [RelayCommand]
    private void CloseWhatsNew()
    {
        IsWhatsNewOpen = false;
    }

    [RelayCommand]
    private void RestorePlayer()
    {
        IsPlayerMinimized = false;
    }

    /// <summary>Raised when user closes the PiP mini-player.</summary>
    public event Action? ClosePipRequested;

    [RelayCommand]
    private void ClosePip()
    {
        ClosePipRequested?.Invoke();
    }

    [RelayCommand]
    private void Logout()
    {
        SelectedMovieForInfo = null;
        LogoutRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenSwitchAccount()
    {
        SwitchingAccountError = string.Empty;
        IsSwitchAccountOpen = true;
    }

    [RelayCommand]
    private void CloseSwitchAccount()
    {
        IsSwitchAccountOpen = false;
    }

    [RelayCommand]
    private async Task SwitchToAccountAsync(UserCredentials creds)
    {
        if (creds == null || IsSwitchingAccount) return;
        IsSwitchingAccount = true;
        SwitchingAccountError = string.Empty;
        SwitchingAccountStatus = $"Switching to {creds.Username}...";

        try
        {
            // Close active playback before switching
            ClosePipRequested?.Invoke();

            var auth = await _api.AuthenticateAsync(creds);
            if (!auth.IsAuthenticated)
            {
                SwitchingAccountError = "Authentication failed. The account might be expired or invalid.";
                IsSwitchingAccount = false;
                return;
            }

            // Update global credentials
            App.CurrentCredentials = creds;

            // Save as default account in settings
            _appSettings.DefaultAccountUsername = creds.Username;
            _appSettings.DefaultAccountServerUrl = creds.ServerUrl;
            
            // Move current creds to the top of SavedAccounts or ensure it's there
            var existing = _appSettings.SavedAccounts.FirstOrDefault(a => 
                a.Username.Equals(creds.Username, StringComparison.OrdinalIgnoreCase) && 
                a.ServerUrl.TrimEnd('/').Equals(creds.ServerUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Password = creds.Password;
            }
            else
            {
                _appSettings.SavedAccounts.Add(creds);
            }
            
            Helpers.CredentialStore.Save(_appSettings);

            // Re-load settings to refresh the SavedAccounts list in the ViewModel
            LoadSettings();

            // Clear view model state & collections
            DisplayUsername = creds.Username;
            ServerDisplay = new Uri(creds.BaseUrl).Host;

            SelectedCategory = null;
            CurrentSeries = null;
            SelectedMovieForInfo = null;
            SelectedSeriesForInfo = null;

            Streams.Clear();
            Categories.Clear();
            FeaturedItems.Clear();
            FeaturedLive.Clear();
            FeaturedShows.Clear();
            FeaturedMovies.Clear();
            DevRecommendations.Clear();
            RecentlyWatched.Clear();
            ContinueWatching.Clear();
            LikedShows.Clear();
            LikedLive.Clear();
            LikedMovies.Clear();
            WatchLater.Clear();

            // Reload data
            await InitializeWhatsNewAsync();
            await LoadCategoriesAsync(forceRefresh: true);

            // Navigate to Home
            ActiveSection = MediaSection.Home;
            IsSwitchAccountOpen = false;
        }
        catch (Exception ex)
        {
            SwitchingAccountError = $"Failed to switch account: {ex.Message}";
        }
        finally
        {
            IsSwitchingAccount = false;
        }
    }

    // ── Download commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleDownloadsTray() => ShowDownloadsTray = !ShowDownloadsTray;

    [RelayCommand]
    private void ClearCompletedDownloads()
    {
        foreach (var d in Downloads.Where(d => !d.IsActive).ToList())
            Downloads.Remove(d);
    }

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        var dir = DownloadFolder;
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    [RelayCommand]
    private void Download(StreamItem stream)
    {
        var c = App.CurrentCredentials!;
        var url = stream.StreamType switch
        {
            "movie"  => $"{c.BaseUrl}/movie/{c.Username}/{c.Password}/{stream.EffectiveStreamId}.{stream.ContainerExtension}",
            "series" => $"{c.BaseUrl}/series/{c.Username}/{c.Password}/{stream.EffectiveStreamId}.{stream.ContainerExtension}",
            _        => null
        };
        if (url is null) return;

        var dir = DownloadFolder;
        Directory.CreateDirectory(dir);

        var safeName = SanitizeFileName(stream.Name);
        var destPath = Path.Combine(dir, $"{safeName}.{stream.ContainerExtension}");

        var groupName = "Movies";
        string? showName = null;
        if (stream.StreamType == "series" && CurrentSeries != null)
        {
            showName = CurrentSeries.Info.Name;
            groupName = showName;
        }

        var item = new DownloadItem
        {
            Name      = stream.Name,
            Url       = url,
            Extension = stream.ContainerExtension,
            DestPath  = destPath,
            GroupName = groupName,
            ShowName  = showName
        };

        Downloads.Add(item);
        ActiveSection = MediaSection.Downloads;

        _ = RunDownloadTaskAsync(item);
    }

    private async Task RunDownloadTaskAsync(DownloadItem item)
    {
        var sem = _downloadSemaphore;
        try
        {
            await sem.WaitAsync(item.Cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // Cancelled/Paused while waiting in queue
        }

        try
        {
            item.Status     = DownloadStatus.Downloading;
            item.StatusText = "Downloading…";

            var prog = new Progress<(long dl, long total, double bps)>(p =>
            {
                item.Progress  = p.total > 0 ? (double)p.dl / p.total : 0;
                item.SpeedText = p.bps > 0 ? $"{p.bps / 1_048_576:F1} MB/s" : string.Empty;
                item.SizeText  = p.total > 0
                    ? $"{p.dl / 1_048_576:N0} / {p.total / 1_048_576:N0} MB"
                    : $"{p.dl / 1_048_576:N0} MB";

                if (item.IsAppUpdate)
                {
                    UpdateDownloadProgress = item.Progress;
                    UpdateDownloadStatus = p.total > 0
                        ? $"Downloading update... {UpdateDownloadProgress * 100:F0}%"
                        : $"Downloading update... {p.dl / 1_048_576:N0} MB";
                }

                OnPropertyChanged(nameof(ActiveDownloadCount));
                OnPropertyChanged(nameof(HasActiveDownloads));
            });

            await _api.DownloadStreamAsync(item.Url, item.DestPath, prog, () => item.SpeedLimitKbps, item.Cts.Token);

            item.Status     = DownloadStatus.Completed;
            item.StatusText = "✔ Complete";
            item.Progress   = 1.0;
            item.SpeedText  = string.Empty;

            if (item.IsAppUpdate)
            {
                item.StatusText = "Launching installer...";
                UpdateDownloadStatus = "Launching installer...";
                await Task.Delay(1500);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.DestPath,
                    UseShellExecute = true
                });

                Application.Current?.Shutdown();
            }
        }
        catch (OperationCanceledException)
        {
            if (item.Status == DownloadStatus.Paused)
            {
                item.StatusText = "॥ Paused";
                item.SpeedText  = string.Empty;
                if (item.IsAppUpdate)
                {
                    UpdateDownloadStatus = "Update paused";
                }
            }
            else
            {
                item.Status     = DownloadStatus.Cancelled;
                item.StatusText = "✕ Stopped";
                item.SpeedText  = string.Empty;
                if (item.IsAppUpdate)
                {
                    IsDownloadingUpdate = false;
                    UpdateDownloadStatus = "Update stopped";
                }
            }
        }
        catch (Exception ex)
        {
            item.Status     = DownloadStatus.Failed;
            item.StatusText = $"Failed: {ex.Message[..Math.Min(ex.Message.Length, 60)]}";
            item.SpeedText  = string.Empty;
            if (item.IsAppUpdate)
            {
                IsDownloadingUpdate = false;
                UpdateDownloadStatus = "Download failed";
                UpdateStatus = $"⚠ Update failed: {ex.Message[..System.Math.Min(ex.Message.Length, 40)]}";
            }
        }
        finally
        {
            sem.Release();
            OnPropertyChanged(nameof(ActiveDownloadCount));
            OnPropertyChanged(nameof(HasActiveDownloads));
        }
    }

    [RelayCommand]
    private void PauseDownload(DownloadItem item)
    {
        if (item.Status != DownloadStatus.Downloading && item.Status != DownloadStatus.Queued) return;
        item.Status = DownloadStatus.Paused;
        item.Cts.Cancel();
    }

    [RelayCommand]
    private void ResumeDownload(DownloadItem item)
    {
        if (item.Status != DownloadStatus.Paused) return;
        item.Status     = DownloadStatus.Queued;
        item.StatusText = "Queued for resume…";
        item.Cts.Dispose();
        item.Cts = new CancellationTokenSource();
        _ = RunDownloadTaskAsync(item);
    }

    [RelayCommand]
    private void StopDownload(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Completed) return;
        item.Status = DownloadStatus.Cancelled;
        item.Cts.Cancel();
        item.StatusText = "✕ Stopped";

        var partPath = item.DestPath + ".part";
        try
        {
            if (File.Exists(partPath)) File.Delete(partPath);
        }
        catch { }

        if (item.IsAppUpdate)
        {
            IsDownloadingUpdate = false;
            UpdateDownloadStatus = "Update stopped";
        }
    }

    [RelayCommand]
    private void CancelDownload(DownloadItem item)
    {
        item.Status = DownloadStatus.Cancelled;
        item.Cts.Cancel();

        var partPath = item.DestPath + ".part";
        try
        {
            if (File.Exists(partPath)) File.Delete(partPath);
        }
        catch { }

        Downloads.Remove(item);

        if (item.IsAppUpdate)
        {
            IsDownloadingUpdate = false;
            UpdateDownloadStatus = "Update stopped";
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c))
                     .Trim().TrimEnd('.');
    }

    private System.Windows.Threading.DispatcherTimer? _carouselTimer;
    private int _carouselIndex = 0;

    private void StartCarouselTimer()
    {
        if (_carouselTimer == null)
        {
            _carouselTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _carouselTimer.Tick += (s, e) => RotateCarousel();
        }
        _carouselTimer.Start();
    }

    private void StopCarouselTimer()
    {
        _carouselTimer?.Stop();
    }

    private void RotateCarousel()
    {
        if (_featuredItems.Count == 0)
        {
            FeaturedCarouselItem = null;
            return;
        }
        _carouselIndex = (_carouselIndex + 1) % _featuredItems.Count;
        FeaturedCarouselItem = _featuredItems[_carouselIndex];
    }

    [RelayCommand]
    private void SelectCarouselItem(StreamItem? item)
    {
        if (item == null) return;
        FeaturedCarouselItem = item;
        _carouselIndex = _featuredItems.IndexOf(item);
        _carouselTimer?.Stop();
        _carouselTimer?.Start();
    }

    private void SyncFeaturedItemsCollection()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            FeaturedItems.Clear();
            FeaturedLive.Clear();
            FeaturedShows.Clear();
            FeaturedMovies.Clear();
            DevRecommendations.Clear();

            foreach (var item in _featuredItems)
            {
                FeaturedItems.Add(item);

                if (item.IsDevRecommendation)
                {
                    DevRecommendations.Add(item);
                }
                else if (item.StreamType == "live")
                {
                    FeaturedLive.Add(item);
                }
                else if (item.SeriesId > 0 || item.StreamType == "series")
                {
                    FeaturedShows.Add(item);
                }
                else
                {
                    FeaturedMovies.Add(item);
                }
            }
        });
    }

    [RelayCommand]
    private void ToggleFeatured(StreamItem stream)
    {
        if (stream is null) return;

        stream.IsFeatured = !stream.IsFeatured;
        var key = GetFeaturedKey(stream);

        if (stream.IsFeatured)
        {
            _featuredKeys.Add(key);
            _featuredItems.Add(stream);
            SyncFeaturedItemsCollection();

            if (FeaturedCarouselItem == null)
            {
                _carouselIndex = 0;
                FeaturedCarouselItem = stream;
                StartCarouselTimer();
            }
        }
        else
        {
            _featuredKeys.Remove(key);
            var existing = _featuredItems.FirstOrDefault(i => GetFeaturedKey(i) == key);
            if (existing != null)
            {
                existing.IsDevRecommendation = false;
                _featuredItems.Remove(existing);
                SyncFeaturedItemsCollection();
            }

            if (_featuredItems.Count == 0)
            {
                FeaturedCarouselItem = null;
                StopCarouselTimer();
            }
            else if (FeaturedCarouselItem == existing)
            {
                _carouselIndex = 0;
                FeaturedCarouselItem = _featuredItems[0];
            }
        }

        SaveWhatsNew();

        if (ActiveSection == MediaSection.WhatsNew && CurrentSeries == null)
        {
            _filteredStreams.Remove(stream);
            _allStreams.Remove(stream);
            UpdatePage();
            StatusMessage = $"{_filteredStreams.Count} featured items";
        }
    }

    [RelayCommand]
    private void ToggleDevRecommendation(StreamItem stream)
    {
        if (stream is null) return;

        stream.IsDevRecommendation = !stream.IsDevRecommendation;

        var key = GetFeaturedKey(stream);
        var existing = _featuredItems.FirstOrDefault(i => GetFeaturedKey(i) == key);
        if (stream.IsDevRecommendation)
        {
            if (existing == null)
            {
                stream.IsFeatured = true;
                _featuredKeys.Add(key);
                _featuredItems.Add(stream);

                if (FeaturedCarouselItem == null)
                {
                    _carouselIndex = 0;
                    FeaturedCarouselItem = stream;
                    StartCarouselTimer();
                }
            }
            else
            {
                existing.IsDevRecommendation = true;
            }
        }
        else
        {
            if (existing != null)
            {
                existing.IsDevRecommendation = false;
            }
        }

        SyncFeaturedItemsCollection();
        SaveWhatsNew();
    }

    private string GetFeaturedKey(StreamItem stream)
    {
        var isSeries = stream.SeriesId != 0 && stream.StreamId == 0;
        var type = isSeries ? "series" : stream.StreamType;
        var id = isSeries ? stream.SeriesId : stream.EffectiveStreamId;
        return $"{type}_{id}";
    }

    private void MarkFeatured(StreamItem stream)
    {
        var key = GetFeaturedKey(stream);
        var existing = _featuredItems.FirstOrDefault(i => GetFeaturedKey(i) == key);
        if (existing != null)
        {
            stream.IsFeatured = true;
            stream.IsDevRecommendation = existing.IsDevRecommendation;
        }
        else
        {
            stream.IsFeatured = false;
            stream.IsDevRecommendation = false;
        }
        
        SyncItemLibraryState(stream);
    }

    private async Task LoadHomeDynamicDataAsync(bool forceRefresh = false)
    {
        if (IsLoadingHomeData) return;
        IsLoadingHomeData = true;

        try
        {
            // Clear current lists immediately on UI thread
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                FeaturedMovies.Clear();
                FeaturedShows.Clear();
                FeaturedLive.Clear();
            });

            // Fetch movies, series, and live streams in parallel
            var moviesTask = _api.GetVodStreamsAsync(null, forceRefresh: forceRefresh);
            var seriesTask = _api.GetSeriesAsync(null, forceRefresh: forceRefresh);
            var liveTask = _api.GetLiveStreamsAsync(null, forceRefresh: forceRefresh);

            await Task.WhenAll(moviesTask, seriesTask, liveTask);

            var movies = await moviesTask ?? new List<StreamItem>();
            var series = await seriesTask ?? new List<StreamItem>();
            var live = await liveTask ?? new List<StreamItem>();

            // Popular Movies: contains (2026), rating > 7.0
            var filteredMovies = movies.Where(m =>
                !string.IsNullOrEmpty(m.Name) &&
                m.Name.Contains("(2026)", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(m.Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r) &&
                r > 7.0
            ).ToList();

            // Popular Shows / TV Series: contains (2026), rating > 7.0
            var filteredSeries = series.Where(s =>
                !string.IsNullOrEmpty(s.Name) &&
                s.Name.Contains("(2026)", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(s.Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r) &&
                r > 7.0
            ).ToList();

            // Featured Live TV: contains "Alwan" or "Al Wan"
            var filteredLive = live.Where(l =>
                !string.IsNullOrEmpty(l.Name) &&
                (l.Name.Contains("Alwan", StringComparison.OrdinalIgnoreCase) ||
                 l.Name.Contains("Al Wan", StringComparison.OrdinalIgnoreCase))
            ).ToList();

            // Fallback for Movies: relax criteria to 2025/2026 if none found
            if (filteredMovies.Count == 0)
            {
                filteredMovies = movies.Where(m =>
                    !string.IsNullOrEmpty(m.Name) &&
                    (m.Name.Contains("(2025)", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("(2026)", StringComparison.OrdinalIgnoreCase)) &&
                    double.TryParse(m.Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r) &&
                    r > 7.0
                ).ToList();
            }
            if (filteredMovies.Count == 0)
            {
                filteredMovies = movies.Where(m =>
                    double.TryParse(m.Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r) &&
                    r > 7.0
                ).ToList();
            }

            // Fallback for Series: relax criteria to 2025/2026 if none found
            if (filteredSeries.Count == 0)
            {
                filteredSeries = series.Where(s =>
                    !string.IsNullOrEmpty(s.Name) &&
                    (s.Name.Contains("(2025)", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("(2026)", StringComparison.OrdinalIgnoreCase)) &&
                    double.TryParse(s.Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r) &&
                    r > 7.0
                ).ToList();
            }
            if (filteredSeries.Count == 0)
            {
                filteredSeries = series.Where(s =>
                    double.TryParse(s.Rating, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r) &&
                    r > 7.0
                ).ToList();
            }

            // Randomly select up to 20 items
            var random = new Random();
            var selectedMovies = filteredMovies.OrderBy(_ => random.Next()).Take(20).ToList();
            var selectedSeries = filteredSeries.OrderBy(_ => random.Next()).Take(20).ToList();

            // Populate on the UI thread
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (var m in selectedMovies) FeaturedMovies.Add(m);
                foreach (var s in selectedSeries) FeaturedShows.Add(s);
                foreach (var l in filteredLive) FeaturedLive.Add(l);
            });
        }
        catch (Exception ex)
        {
            LogService.Log("Error loading home page dynamic content", ex);
        }
        finally
        {
            IsLoadingHomeData = false;
        }
    }

    private async Task InitializeWhatsNewAsync()
    {
        LoadLibraryData();

        await LoadWhatsNewAsync();
        
        if (ActiveSection == MediaSection.Home)
        {
            _ = LoadHomeDynamicDataAsync();
        }
        else
        {
            SyncFeaturedItemsCollection();
        }

        if (_featuredItems.Count > 0)
        {
            _carouselIndex = 0;
            FeaturedCarouselItem = _featuredItems[0];
            Application.Current?.Dispatcher.BeginInvoke(() => StartCarouselTimer());
        }

        LoadRecentlyWatched();

        if (ActiveSection == MediaSection.WhatsNew)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var featured = _featuredItems.ToList();
                foreach (var item in featured)
                {
                    item.IsFeatured = true;
                }
                _allStreams = featured;
                SetFilteredStreams(featured);
                StatusMessage = $"{featured.Count} featured items";
            });
        }
    }

    private const string WhatsNewRemoteUrl = 
        "https://raw.githubusercontent.com/KyuJunior/IPXtream/main/whats_new.json";

    private async Task LoadWhatsNewAsync()
    {
        try
        {
            _featuredItems.Clear();
            _featuredKeys.Clear();
            string json = string.Empty;

#if DEBUG
            // In DEBUG/Admin mode, load from the repository root whats_new.json if it exists to allow editing
            var repoPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\whats_new.json"));
            if (File.Exists(repoPath))
            {
                json = File.ReadAllText(repoPath);
            }
#endif

            if (string.IsNullOrEmpty(json))
            {
                try
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("IPXtream/1.0");
                    json = await http.GetStringAsync(WhatsNewRemoteUrl);
                    
                    var cachePath = GetWhatsNewFilePath();
                    File.WriteAllText(cachePath, json);
                }
                catch
                {
                    var cachePath = GetWhatsNewFilePath();
                    if (File.Exists(cachePath))
                    {
                        json = File.ReadAllText(cachePath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        item.IsFeatured = true;
                        _featuredItems.Add(item);
                        _featuredKeys.Add(GetFeaturedKey(item));
                    }
                }
            }
        }
        catch { }
    }

    private void SaveWhatsNew()
    {
        try
        {
            var path = GetWhatsNewFilePath();
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_featuredItems, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(path, json);

#if DEBUG
            // In DEBUG/Admin mode, also save to the repository root whats_new.json so changes can be committed
            var repoPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\whats_new.json"));
            var repoDir = Path.GetDirectoryName(repoPath);
            if (repoDir != null && Directory.Exists(repoDir))
            {
                File.WriteAllText(repoPath, json);
            }
#endif
        }
        catch { }
    }

    public event Action<TaskCompletionSource<string?>>? RequestGithubToken;

    public void ToggleAdminMode()
    {
        IsAdminMode = !IsAdminMode;
    }

    [RelayCommand]
    private async Task PushWhatsNewAsync()
    {
        try
        {
            var settings = Helpers.CredentialStore.Load();
            string? token = settings.GithubToken;

            if (string.IsNullOrWhiteSpace(token))
            {
                var tcs = new TaskCompletionSource<string?>();
                RequestGithubToken?.Invoke(tcs);
                token = await tcs.Task;

                if (string.IsNullOrWhiteSpace(token))
                {
                    StatusMessage = "Push cancelled. GitHub token is required.";
                    return;
                }

                settings.GithubToken = token;
                Helpers.CredentialStore.Save(settings);
            }

            StatusMessage = "Syncing with GitHub...";

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_featuredItems, Newtonsoft.Json.Formatting.Indented);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            string base64Content = Convert.ToBase64String(bytes);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IPXtream/1.0");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

            string apiUrl = "https://api.github.com/repos/KyuJunior/IPXtream/contents/whats_new.json";
            
            StatusMessage = "Fetching existing What's New SHA...";
            var response = await client.GetAsync(apiUrl);
            
            string? sha = null;
            if (response.IsSuccessStatusCode)
            {
                string resBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(resBody);
                if (doc.RootElement.TryGetProperty("sha", out var shaProp))
                {
                    sha = shaProp.GetString();
                }
            }

            StatusMessage = "Pushing updates to GitHub...";
            var commitBody = new
            {
                message = "Update What's New from in-app admin panel",
                content = base64Content,
                sha = sha
            };

            string commitJson = Newtonsoft.Json.JsonConvert.SerializeObject(commitBody);
            using var content = new StringContent(commitJson, System.Text.Encoding.UTF8, "application/json");
            
            var putResponse = await client.PutAsync(apiUrl, content);
            if (putResponse.IsSuccessStatusCode)
            {
                StatusMessage = "Push successful! What's New updated for all users.";
                MessageBox.Show("Successfully pushed What's New updates to GitHub! Changes will be live for all users in a few minutes.", "Push Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                string err = await putResponse.Content.ReadAsStringAsync();
                StatusMessage = $"Push failed: {putResponse.StatusCode}";
                if (putResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    settings.GithubToken = null;
                    Helpers.CredentialStore.Save(settings);
                }
                MessageBox.Show($"Failed to push changes to GitHub.\n\nStatus: {putResponse.StatusCode}\nDetails: {err}", "Push Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error pushing to GitHub.";
            MessageBox.Show($"An error occurred during push:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetWhatsNewFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IPXtream");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "whats_new.json");
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (ActiveSection == MediaSection.WhatsNew)
        {
            await LoadWhatsNewAsync();
            SyncFeaturedItemsCollection();
            if (_featuredItems.Count > 0)
            {
                _carouselIndex = 0;
                FeaturedCarouselItem = _featuredItems[0];
                StartCarouselTimer();
            }
            else
            {
                FeaturedCarouselItem = null;
                StopCarouselTimer();
            }

            Streams.Clear();
            _allStreams.Clear();
            foreach (var item in _featuredItems)
            {
                item.IsFeatured = true;
                Streams.Add(item);
                _allStreams.Add(item);
            }
            StatusMessage = $"{Streams.Count} featured items";
            return;
        }

        if (ActiveSection == MediaSection.CurrentlyWatching)
        {
            LoadCurrentlyWatching();
            return;
        }

        if (ActiveSection == MediaSection.Home)
        {
            await LoadHomeDynamicDataAsync(forceRefresh: true);
            return;
        }

        // Force refresh categories (which will then auto-refresh streams if one is selected)
        await LoadCategoriesAsync(forceRefresh: true);
        
        // If a category was already selected, refresh its streams too
        if (SelectedCategory != null)
        {
            await LoadStreamsAsync(SelectedCategory.CategoryId, forceRefresh: true);
        }
    }

    [RelayCommand]
    private async Task CacheAllBackgroundAsync()
    {
        if (IsCachingAll) return;
        IsCachingAll = true;
        _cacheCts    = new CancellationTokenSource();
        var ct       = _cacheCts.Token;

        try
        {
            // ── Phase 1/3: Fetch all stream lists ──────────────────────────────
            StatusMessage = "Caching (1/3) — Fetching Live TV…";
            var liveStreams = await _api.GetLiveStreamsAsync(null, ct, forceRefresh: true);

            StatusMessage = "Caching (1/3) — Fetching Movies…";
            var vodStreams = await _api.GetVodStreamsAsync(null, ct, forceRefresh: true);

            StatusMessage = "Caching (1/3) — Fetching Series list…";
            var seriesItems = await _api.GetSeriesAsync(null, ct, forceRefresh: true);

            // ── Phase 2/3: Thumbnails for fetched streams ──────────────────────
            var imageUrls = liveStreams
                .Concat(vodStreams)
                .Concat(seriesItems)
                .SelectMany(s => new[] { s.StreamIcon, s.Thumbnail ?? string.Empty })
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int imgTotal = imageUrls.Count;
            // Progress<T> captures the UI SynchronizationContext — callback runs on UI thread automatically
            var imgProgress = new Progress<(int done, int total)>(p =>
                StatusMessage = $"Caching (2/3) — Thumbnails {p.done:N0} / {p.total:N0}");

            StatusMessage = $"Caching (2/3) — Downloading {imgTotal:N0} thumbnails…";
            await _api.BulkCacheImagesAsync(imageUrls, imgProgress, ct);

            // ── Phase 3/3: Series episode trees ─────────────────────────────
            var seriesIds = seriesItems
                .Select(s => s.SeriesId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            int serTotal = seriesIds.Count;
            var serProgress = new Progress<(int done, int total)>(p =>
                StatusMessage = $"Caching (3/3) — Series episodes {p.done:N0} / {p.total:N0}");

            StatusMessage = $"Caching (3/3) — Fetching {serTotal:N0} series episode lists…";
            await _api.BulkCacheSeriesInfoAsync(seriesIds, serProgress, ct);

            int streamTotal = liveStreams.Count + vodStreams.Count + seriesItems.Count;
            StatusMessage = $"✔ Cache complete — {streamTotal:N0} streams · {imgTotal:N0} images · {serTotal:N0} series";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cache cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cache failed: {ex.Message}";
        }
        finally
        {
            _cacheCts?.Dispose();
            _cacheCts = null;
            IsCachingAll = false;
        }
    }

    [RelayCommand]
    private void CancelCache() => _cacheCts?.Cancel();

    // ── Category selected ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectCategoryAsync(Category? category)
    {
        if (category is null) return;
        SelectedCategory = category;
        CurrentSeries    = null;
        await LoadStreamsAsync(category.CategoryId);
    }

    // ── Stream double-clicked / Play button ───────────────────────────────────

    [RelayCommand]
    private async Task PlayAsync(StreamItem? stream)
    {
        if (stream is null) return;

        LogService.Log($"PlayAsync: Name={stream.Name}, SeriesId={stream.SeriesId}, StreamId={stream.StreamId}, StreamType={stream.StreamType}, ActiveSection={ActiveSection}");

        // If it's a Series container (has SeriesId and no StreamId),
        // don't play it — fetch and show its episodes instead.
        if (stream.SeriesId != 0 && stream.StreamId == 0)
        {
            await LoadSeriesEpisodesAsync(stream);
            return;
        }

        // Otherwise (LiveTV, VOD, or an Episode of a Series), play it.
        AddToCurrentlyWatching(stream);

        List<StreamItem> siblings = new List<StreamItem>();
        if (stream.StreamType == "series")
        {
            if (ActiveSection == MediaSection.CurrentlyWatching && stream.SeriesId != 0)
            {
                try
                {
                    StatusMessage = "Loading series details...";
                    var info = await _api.GetSeriesInfoAsync(stream.SeriesId);
                    if (info != null)
                    {
                        var epIdStr = stream.StreamId.ToString();
                        List<Episode>? foundSeasonEpisodes = null;

                        foreach (var kvp in info.Episodes)
                        {
                            if (kvp.Value.Any(e => e.Id == epIdStr))
                            {
                                foundSeasonEpisodes = kvp.Value;
                                break;
                            }
                        }

                        if (foundSeasonEpisodes != null)
                        {
                            foreach (var ep in foundSeasonEpisodes)
                            {
                                _ = int.TryParse(ep.Id, out int epId);
                                siblings.Add(new StreamItem
                                {
                                    Name               = string.IsNullOrWhiteSpace(ep.Title) ? ep.Info.Name : ep.Title,
                                    StreamType         = "series",
                                    StreamId           = epId,
                                    VideoId            = epId,
                                    SeriesId           = stream.SeriesId,
                                    ContainerExtension = ep.ContainerExtension ?? "mp4",
                                    Plot               = ep.Info.Plot,
                                    CustomSid          = ep.CustomSid,
                                    StreamIcon         = !string.IsNullOrEmpty(ep.Info.MovieImage) ? ep.Info.MovieImage : (info.Info.Cover ?? string.Empty)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log("Error loading siblings for currently watching episode", ex);
                }
                finally
                {
                    StatusMessage = string.Empty;
                }
            }
            else
            {
                siblings = _allStreams.ToList();
            }
        }

        PlayRequested?.Invoke(stream, siblings);
    }

    public void TriggerPlayRequest(StreamItem stream, List<StreamItem> siblings)
    {
        PlayRequested?.Invoke(stream, siblings);
    }

    [RelayCommand]
    private async Task BackToSeriesAsync()
    {
        CurrentSeries = null;
        SelectedSeriesForInfo = null;
        SelectedMovieForInfo = null;
        if (ActiveSection == MediaSection.WhatsNew)
        {
            var featured = _featuredItems.ToList();
            foreach (var item in featured)
            {
                item.IsFeatured = true;
            }
            _allStreams = featured;
            SetFilteredStreams(featured);
            StatusMessage = $"{featured.Count} featured items";
        }
        else if (SelectedCategory is not null)
        {
            await LoadStreamsAsync(SelectedCategory.CategoryId);
        }
    }

    [RelayCommand]
    private void ShowMovieInfo(StreamItem? movie)
    {
        if (movie is null) return;
        SelectedMovieForInfo = movie;
    }

    [RelayCommand]
    private void CloseMovieInfo()
    {
        SelectedMovieForInfo = null;
    }

    // ── Search ────────────────────────────────────────────────────────────────

    partial void OnSearchTextChanged(string value)
    {
        // Debounce search: reset the timer on every keystroke
        if (_searchTimer == null)
        {
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _searchTimer.Tick += async (s, e) =>
            {
                try
                {
                    _searchTimer.Stop();
                    await PerformSearchAsync(SearchText);
                }
                catch (Exception ex)
                {
                    LogService.Log("Unhandled error during search timer tick.", ex);
                }
            };
        }

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private async Task PerformSearchAsync(string query)
    {
        try
        {
            Streams.Clear();

            // If searching the "All" category without text, we don't want to draw everything
            if (string.IsNullOrWhiteSpace(query) && SelectedCategory?.CategoryId == "" && !IsViewingSeriesInfo)
            {
                _filteredStreams.Clear();
                CurrentPage = 1;
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                OnPropertyChanged(nameof(PageStatusText));
                StatusMessage = $"Loaded {_allStreams.Count} items. Type in the search box to find a stream.";
                return;
            }

            StatusMessage = "Searching...";

            // Filter on a background thread
            var filtered = await Task.Run(() => XtreamApiService.Search(_allStreams, query).ToList());
            
            // Abort if typing changed
            if (SearchText != query) return;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (SearchText == query)
                {
                    SetFilteredStreams(filtered);
                    StatusMessage = filtered.Count == 0 && _allStreams.Count > 0
                        ? "No results match your search."
                        : $"{filtered.Count} / {_allStreams.Count} streams";
                }
            });
        }
        catch (Exception ex)
        {
            LogService.Log("Error performing search query.", ex);
            StatusMessage = "Search failed.";
        }
    }

    // ── Private loaders ───────────────────────────────────────────────────────

    private async Task LoadCategoriesAsync(bool forceRefresh = false)
    {
        if (ActiveSection == MediaSection.WhatsNew || ActiveSection == MediaSection.CurrentlyWatching || ActiveSection == MediaSection.Home) return;
        IsLoadingCategories = true;
        ErrorMessage        = string.Empty;
        Categories.Clear();

        try
        {
            var cats = ActiveSection switch
            {
                MediaSection.VOD    => await _api.GetVodCategoriesAsync(forceRefresh: forceRefresh),
                MediaSection.Series => await _api.GetSeriesCategoriesAsync(forceRefresh: forceRefresh),
                _                   => await _api.GetLiveCategoriesAsync(forceRefresh: forceRefresh)
            };

            // ── Inject synthetic "All" category at the top ───────────────
            var allCats = new List<Category>
            {
                new Category { CategoryId = "", CategoryName = "All" }
            };
            allCats.AddRange(cats);

            Categories.Clear();

            // Yield slightly to keep UI responsive while adding items
            foreach (var c in allCats)
            {
                Categories.Add(c);
            }

            StatusMessage = cats.Count == 0
                ? "No categories found."
                : $"{cats.Count} categories loaded.";
        }
        catch (XtreamApiException ex)
        {
            ErrorMessage  = ex.Message;
            StatusMessage = "Failed to load categories.";
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    private async Task LoadStreamsAsync(string categoryId, bool forceRefresh = false, CancellationToken ct = default)
    {
        if (ActiveSection == MediaSection.WhatsNew || ActiveSection == MediaSection.CurrentlyWatching || ActiveSection == MediaSection.Home) return;
        IsLoadingStreams = true;
        ErrorMessage     = string.Empty;
        Streams.Clear();
        _allStreams.Clear();
        Seasons.Clear();
        CurrentSeries = null;
        SearchText    = string.Empty;

        try
        {
            var items = ActiveSection switch
            {
                MediaSection.VOD    => await _api.GetVodStreamsAsync(categoryId, forceRefresh: forceRefresh, ct: ct),
                MediaSection.Series => await _api.GetSeriesAsync(categoryId, forceRefresh: forceRefresh, ct: ct),
                _                   => await _api.GetLiveStreamsAsync(categoryId, forceRefresh: forceRefresh, ct: ct)
            };

            if (ct.IsCancellationRequested) return;

            foreach (var s in items)
            {
                MarkFeatured(s);
            }

            _allStreams = items;

            // If this is the "All" category, drawing thousands of items freezes the app.
            // We store them in _allStreams but leave the UI empty until they search.
            if (string.IsNullOrEmpty(categoryId))
            {
                SetFilteredStreams(new List<StreamItem>());
                StatusMessage = $"Loaded {items.Count} items. Type in the search box to find a stream.";
            }
            else
            {
                SetFilteredStreams(items);
                StatusMessage = $"{items.Count} streams";
            }
        }
        catch (OperationCanceledException)
        {
            // Silently abort on cancellation
        }
        catch (XtreamApiException ex)
        {
            ErrorMessage  = ex.Message;
            StatusMessage = "Failed to load streams.";
        }
        catch (Exception ex)
        {
            LogService.Log("Unhandled exception loading streams.", ex);
            ErrorMessage  = "An unexpected error occurred while loading streams.";
            StatusMessage = "Failed to load streams.";
        }
        finally
        {
            IsLoadingStreams = false;
        }
    }

    private async Task LoadSeriesEpisodesAsync(StreamItem series, bool forceRefresh = false)
    {
        IsLoadingStreams = true;
        ErrorMessage     = string.Empty;
        SearchText       = string.Empty;
        Streams.Clear();
        _allStreams.Clear();
        Seasons.Clear();

        try
        {
            var info = await _api.GetSeriesInfoAsync(series.SeriesId, forceRefresh: forceRefresh);
            if (info is null) return;

            CurrentSeries = info;
            _currentSeriesId = series.SeriesId;
            SelectedSeriesForInfo = series;
            SyncItemLibraryState(series);

            // Load seasons
            foreach (var kvp in info.Episodes)
            {
                Seasons.Add(new SeasonItem
                {
                    SeasonNumber = kvp.Key,
                    Episodes     = kvp.Value
                });
            }

            // Select first season automatically to populate the episodes list
            SelectedSeason = Seasons.FirstOrDefault();
            LogService.Log($"LoadSeriesEpisodesAsync: CurrentSeries={info.Info.Name}, SeasonsCount={Seasons.Count}, EpisodesCount={(info.Episodes != null ? info.Episodes.Values.Sum(l => l.Count) : 0)}");
        }
        catch (XtreamApiException ex)
        {
            ErrorMessage  = ex.Message;
            StatusMessage = "Failed to load episodes.";
        }
        finally
        {
            IsLoadingStreams = false;
        }
    }

    // ── Settings persistence ──────────────────────────────────────────────────

    private AppSettings _appSettings = new();

    [ObservableProperty]
    private bool _autoLogin;

    [ObservableProperty]
    private int _maxConcurrentDownloads;

    [ObservableProperty]
    private string _defaultContainerExtension = "ts";

    [ObservableProperty]
    private string _selectedPlayerEngine = "Flyleaf";

    [ObservableProperty]
    private string _selectedTheme = "Dark Purple";

    partial void OnSelectedThemeChanged(string value)
    {
        Helpers.ThemeHelper.ApplyTheme(value);
    }

    [ObservableProperty]
    private string _homeCardStyle = "Minimal Gradients & Icons";

    partial void OnHomeCardStyleChanged(string value)
    {
        SaveSettings();
    }

    public System.Collections.ObjectModel.ObservableCollection<UserCredentials> SavedAccounts { get; } = new();

    private void LoadSettings()
    {
        try
        {
            _appSettings = Helpers.CredentialStore.Load();
            DownloadFolder = _appSettings.DownloadFolder;
            AutoLogin = _appSettings.AutoLogin;
            MaxConcurrentDownloads = _appSettings.MaxConcurrentDownloads;
            DefaultContainerExtension = _appSettings.DefaultContainerExtension;
            SelectedPlayerEngine = _appSettings.SelectedPlayerEngine ?? "Flyleaf";
            SelectedTheme = _appSettings.SelectedTheme ?? "Dark Purple";
            HomeCardStyle = _appSettings.HomeCardStyle ?? "Minimal Gradients & Icons";

            SavedAccounts.Clear();
            foreach (var account in _appSettings.SavedAccounts)
            {
                // Determine if this is the default account
                account.IsDefault = (account.Username == _appSettings.DefaultAccountUsername && 
                                     account.ServerUrl == _appSettings.DefaultAccountServerUrl);
                SavedAccounts.Add(account);
            }

            // Sync semaphore limit
            _downloadSemaphore = new SemaphoreSlim(MaxConcurrentDownloads);
        }
        catch
        {
            DownloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "IPXtream");
            AutoLogin = true;
            MaxConcurrentDownloads = 2;
            DefaultContainerExtension = "ts";
            SelectedPlayerEngine = "Flyleaf";
            SelectedTheme = "Dark Purple";
            HomeCardStyle = "Minimal Gradients & Icons";
        }
    }

    private void SaveSettings()
    {
        try
        {
            _appSettings.DownloadFolder = DownloadFolder;
            _appSettings.AutoLogin = AutoLogin;
            
            // If MaxConcurrentDownloads changed, re-create the semaphore
            if (_appSettings.MaxConcurrentDownloads != MaxConcurrentDownloads)
            {
                _appSettings.MaxConcurrentDownloads = MaxConcurrentDownloads;
                _downloadSemaphore = new SemaphoreSlim(MaxConcurrentDownloads);
            }

            _appSettings.DefaultContainerExtension = DefaultContainerExtension;
            _appSettings.SelectedPlayerEngine = SelectedPlayerEngine;
            _appSettings.SelectedTheme = SelectedTheme;
            _appSettings.HomeCardStyle = HomeCardStyle;

            Helpers.CredentialStore.Save(_appSettings);
        }
        catch
        {
            // Ignore settings save errors
        }
    }

    [RelayCommand]
    private void ChangeDownloadFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Local Download Location",
            InitialDirectory = Directory.Exists(DownloadFolder) ? DownloadFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            DownloadFolder = dialog.FolderName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void SetDefaultAccount(UserCredentials creds)
    {
        if (creds == null) return;

        _appSettings.DefaultAccountUsername = creds.Username;
        _appSettings.DefaultAccountServerUrl = creds.ServerUrl;
        
        Helpers.CredentialStore.Save(_appSettings);
        
        // Refresh IsDefault property on accounts list
        foreach (var account in SavedAccounts)
        {
            account.IsDefault = (account.Username == _appSettings.DefaultAccountUsername && 
                                 account.ServerUrl == _appSettings.DefaultAccountServerUrl);
        }
    }

    [RelayCommand]
    private void RemoveAccount(UserCredentials creds)
    {
        if (creds == null) return;

        var existing = _appSettings.SavedAccounts.FirstOrDefault(a => 
            a.Username.Equals(creds.Username, StringComparison.OrdinalIgnoreCase) && 
            a.ServerUrl.TrimEnd('/').Equals(creds.ServerUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            
        if (existing != null)
        {
            _appSettings.SavedAccounts.Remove(existing);
            SavedAccounts.Remove(creds);

            if (creds.Username == _appSettings.DefaultAccountUsername && creds.ServerUrl == _appSettings.DefaultAccountServerUrl)
            {
                var nextDefault = _appSettings.SavedAccounts.FirstOrDefault();
                _appSettings.DefaultAccountUsername = nextDefault?.Username;
                _appSettings.DefaultAccountServerUrl = nextDefault?.ServerUrl;
            }

            Helpers.CredentialStore.Save(_appSettings);
            
            // Refresh IsDefault property on remaining accounts
            foreach (var account in SavedAccounts)
            {
                account.IsDefault = (account.Username == _appSettings.DefaultAccountUsername && 
                                     account.ServerUrl == _appSettings.DefaultAccountServerUrl);
            }
        }
    }

    [RelayCommand]
    private void AddNewAccount()
    {
        NewAccountErrorMessage = string.Empty;

        var url = NewAccountServerUrl?.Trim();
        var user = NewAccountUsername?.Trim();
        var pass = NewAccountPassword;

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            NewAccountErrorMessage = "All fields (Server URL, Username, Password) are required.";
            return;
        }

        // Basic URL corrections and checks
        if (url.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url.Substring(7);
        }
        else if (url.StartsWith("HTTPS://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url.Substring(8);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            NewAccountErrorMessage = "Server URL is not a valid absolute URL.";
            return;
        }

        // Check if account already exists
        var existing = _appSettings.SavedAccounts.FirstOrDefault(a => 
            a.Username.Equals(user, StringComparison.OrdinalIgnoreCase) && 
            a.ServerUrl.TrimEnd('/').Equals(url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            NewAccountErrorMessage = "This account already exists.";
            return;
        }

        var creds = new UserCredentials
        {
            ServerUrl = url,
            Username = user,
            Password = pass,
            RememberMe = true
        };

        _appSettings.SavedAccounts.Add(creds);
        
        // If this is the only account, set it as default
        if (string.IsNullOrEmpty(_appSettings.DefaultAccountUsername))
        {
            _appSettings.DefaultAccountUsername = creds.Username;
            _appSettings.DefaultAccountServerUrl = creds.ServerUrl;
        }

        Helpers.CredentialStore.Save(_appSettings);

        // Update active collections
        creds.IsDefault = (creds.Username == _appSettings.DefaultAccountUsername && 
                           creds.ServerUrl == _appSettings.DefaultAccountServerUrl);
        SavedAccounts.Add(creds);

        // Clear forms
        NewAccountServerUrl = string.Empty;
        NewAccountUsername = string.Empty;
        NewAccountPassword = string.Empty;
    }

    [RelayCommand]
    private void ApplyGeneralSettings()
    {
        SaveSettings();
    }

    [RelayCommand]
    private void ToggleLike(StreamItem? stream)
    {
        if (stream == null) return;
        
        stream.IsLiked = !stream.IsLiked;
        
        if (stream.StreamType == "movie")
        {
            ToggleItemInCollection(LikedMovies, stream, stream.IsLiked, "liked_movies.json");
        }
        else if (stream.StreamType == "series")
        {
            ToggleItemInCollection(LikedShows, stream, stream.IsLiked, "liked_shows.json");
        }
        else if (stream.StreamType == "live" || string.IsNullOrEmpty(stream.StreamType))
        {
            // Fallback for live channels or empty stream type (which default to live in player)
            ToggleItemInCollection(LikedLive, stream, stream.IsLiked, "liked_live.json");
        }

        // Keep all other references in sync
        SyncAllItemsLibraryState();
    }

    [RelayCommand]
    private void ToggleWatchLater(StreamItem? stream)
    {
        if (stream == null || stream.StreamType == "live") return;
        
        stream.IsWatchLater = !stream.IsWatchLater;
        ToggleItemInCollection(WatchLater, stream, stream.IsWatchLater, "watch_later.json");

        // Keep all other references in sync
        SyncAllItemsLibraryState();
    }

    private void ToggleItemInCollection(ObservableCollection<StreamItem> collection, StreamItem stream, bool isAdded, string fileName)
    {
        var existing = collection.FirstOrDefault(x => x.EffectiveStreamId == stream.EffectiveStreamId && x.StreamType == stream.StreamType);
        if (isAdded)
        {
            if (existing == null)
            {
                // Sync library status before adding to the collection
                SyncItemLibraryState(stream);
                collection.Insert(0, stream);
            }
        }
        else
        {
            if (existing != null)
            {
                collection.Remove(existing);
            }
        }
        SaveLibraryList(collection, fileName);
    }

    private void SaveLibraryList(ObservableCollection<StreamItem> collection, string fileName)
    {
        try
        {
            var filePath = GetLibraryFilePath(fileName);
            var itemsList = collection.ToList();
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(itemsList, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            LogService.Log($"Error saving library list to {fileName}", ex);
        }
    }

    private string GetLibraryFilePath(string fileName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "IPXtream");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    private void LoadLibrarySection(ObservableCollection<StreamItem> collection, string fileName)
    {
        try
        {
            var filePath = GetLibraryFilePath(fileName);
            List<StreamItem> list;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json) ?? new List<StreamItem>();
            }
            else
            {
                list = new List<StreamItem>();
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                collection.Clear();
                foreach (var item in list)
                {
                    collection.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            LogService.Log($"Error loading library section from {fileName}", ex);
        }
    }

    private void LoadLibraryData()
    {
        // 1. Load Continue Watching from currently_watching.json
        LoadLibrarySection(ContinueWatching, "currently_watching.json");
        // 2. Load Liked Shows from liked_shows.json
        LoadLibrarySection(LikedShows, "liked_shows.json");
        // 3. Load Liked Live TV from liked_live.json
        LoadLibrarySection(LikedLive, "liked_live.json");
        // 4. Load Liked Movies from liked_movies.json
        LoadLibrarySection(LikedMovies, "liked_movies.json");
        // 5. Load Watch Later from watch_later.json
        LoadLibrarySection(WatchLater, "watch_later.json");

        // Sync IsLiked/IsWatchLater properties on loaded items
        SyncAllItemsLibraryState();

        // Also merge them into _allStreams for searching
        try
        {
            var tempAll = new List<StreamItem>();
            void LoadAndMergeFile(string file)
            {
                var filePath = GetLibraryFilePath(file);
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (!tempAll.Any(x => x.EffectiveStreamId == item.EffectiveStreamId && x.StreamType == item.StreamType))
                            {
                                tempAll.Add(item);
                            }
                        }
                    }
                }
            }

            LoadAndMergeFile("currently_watching.json");
            LoadAndMergeFile("liked_shows.json");
            LoadAndMergeFile("liked_live.json");
            LoadAndMergeFile("liked_movies.json");
            LoadAndMergeFile("watch_later.json");

            _allStreams = tempAll;
            foreach (var item in _allStreams)
            {
                SyncItemLibraryState(item);
            }
            SetFilteredStreams(_allStreams);
            StatusMessage = $"{tempAll.Count} unique items in My Library";
        }
        catch (Exception ex)
        {
            LogService.Log("Error loading library items for search", ex);
        }
    }

    private void SyncItemLibraryState(StreamItem stream)
    {
        if (stream == null) return;

        // Determine if it's liked
        if (stream.StreamType == "movie")
        {
            stream.IsLiked = LikedMovies.Any(x => x.EffectiveStreamId == stream.EffectiveStreamId);
        }
        else if (stream.StreamType == "series")
        {
            stream.IsLiked = LikedShows.Any(x => x.SeriesId == stream.SeriesId || (x.EffectiveStreamId == stream.EffectiveStreamId && x.EffectiveStreamId != 0));
        }
        else if (stream.StreamType == "live" || string.IsNullOrEmpty(stream.StreamType))
        {
            stream.IsLiked = LikedLive.Any(x => x.EffectiveStreamId == stream.EffectiveStreamId);
        }

        // Determine if it is watch later (only movies and series)
        if (stream.StreamType == "movie" || stream.StreamType == "series")
        {
            stream.IsWatchLater = WatchLater.Any(x => x.EffectiveStreamId == stream.EffectiveStreamId || (stream.StreamType == "series" && x.SeriesId == stream.SeriesId && x.SeriesId != 0));
        }
    }

    private void SyncAllItemsLibraryState()
    {
        // Sync continue watching items
        foreach (var item in ContinueWatching) SyncItemLibraryState(item);
        // Sync liked items (liked shows, liked movies, liked live tv)
        foreach (var item in LikedShows) SyncItemLibraryState(item);
        foreach (var item in LikedLive) SyncItemLibraryState(item);
        foreach (var item in LikedMovies) SyncItemLibraryState(item);
        // Sync watch later items
        foreach (var item in WatchLater) SyncItemLibraryState(item);

        // Sync currently active lists
        foreach (var item in Streams) SyncItemLibraryState(item);
        foreach (var item in RecentlyWatched) SyncItemLibraryState(item);
        foreach (var item in FeaturedItems) SyncItemLibraryState(item);
        foreach (var item in FeaturedLive) SyncItemLibraryState(item);
        foreach (var item in FeaturedShows) SyncItemLibraryState(item);
        foreach (var item in FeaturedMovies) SyncItemLibraryState(item);
        foreach (var item in DevRecommendations) SyncItemLibraryState(item);

        if (SelectedSeriesForInfo != null) SyncItemLibraryState(SelectedSeriesForInfo);
        if (SelectedMovieForInfo != null) SyncItemLibraryState(SelectedMovieForInfo);
    }

    private string GetCurrentlyWatchingFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "IPXtream");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "currently_watching.json");
    }

    private void AddToCurrentlyWatching(StreamItem stream)
    {
        try
        {
            var filePath = GetCurrentlyWatchingFilePath();
            List<StreamItem> list;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json) ?? new List<StreamItem>();
            }
            else
            {
                list = new List<StreamItem>();
            }

            // Remove if already exists (to push to the top)
            list.RemoveAll(x => x.EffectiveStreamId == stream.EffectiveStreamId && x.StreamType == stream.StreamType);

            // Add to start (most recent)
            list.Insert(0, stream);

            // Limit to 50 items
            if (list.Count > 50)
            {
                list = list.Take(50).ToList();
            }

            var newJson = Newtonsoft.Json.JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, newJson);
            LoadRecentlyWatched();
            LoadLibrarySection(ContinueWatching, "currently_watching.json");
        }
        catch (Exception ex)
        {
            LogService.Log("Error saving to currently watching", ex);
        }
    }

    private void LoadCurrentlyWatching()
    {
        try
        {
            var filePath = GetCurrentlyWatchingFilePath();
            List<StreamItem> list;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json) ?? new List<StreamItem>();
            }
            else
            {
                list = new List<StreamItem>();
            }

            _allStreams = list;
            SetFilteredStreams(list);
            StatusMessage = $"{list.Count} items in Currently Watching";
        }
        catch (Exception ex)
        {
            LogService.Log("Error loading currently watching", ex);
            _allStreams = new List<StreamItem>();
            SetFilteredStreams(_allStreams);
            StatusMessage = "Failed to load Currently Watching items";
        }
    }

    private void LoadRecentlyWatched()
    {
        try
        {
            var filePath = GetCurrentlyWatchingFilePath();
            List<StreamItem> list;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json) ?? new List<StreamItem>();
            }
            else
            {
                list = new List<StreamItem>();
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                RecentlyWatched.Clear();
                foreach (var item in list.Take(10))
                {
                    // Mock progress if it is 0
                    if (item.WatchProgress == 0)
                    {
                        var rand = new Random(item.EffectiveStreamId);
                        item.WatchProgress = rand.Next(15, 85);
                        item.WatchProgressText = item.StreamType == "series"
                            ? $"{item.SeasonName ?? "Season 1"} {item.EpisodeTitle ?? item.Name}, {Math.Round(item.WatchProgress)}% watched"
                            : $"{Math.Round(item.WatchProgress)}% watched";
                    }
                    RecentlyWatched.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            LogService.Log("Error loading recently watched", ex);
        }
    }

    private DateTime _lastProgressSaveTime = DateTime.MinValue;

    public void UpdateStreamProgress(StreamItem stream, double progress)
    {
        if (stream == null || stream.StreamType == "live") return;

        var text = stream.StreamType == "series"
            ? $"{stream.SeasonName ?? "Season 1"} {stream.EpisodeTitle ?? stream.Name}, {Math.Round(progress)}% watched"
            : $"{Math.Round(progress)}% watched";

        var existing = RecentlyWatched.FirstOrDefault(x => x.EffectiveStreamId == stream.EffectiveStreamId && x.StreamType == stream.StreamType);
        if (existing != null)
        {
            existing.WatchProgress = progress;
            existing.WatchProgressText = text;
        }

        var existingCw = ContinueWatching.FirstOrDefault(x => x.EffectiveStreamId == stream.EffectiveStreamId && x.StreamType == stream.StreamType);
        if (existingCw != null)
        {
            existingCw.WatchProgress = progress;
            existingCw.WatchProgressText = text;
        }

        if ((DateTime.Now - _lastProgressSaveTime).TotalSeconds > 3)
        {
            _lastProgressSaveTime = DateTime.Now;
            SaveCurrentlyWatchingProgress(stream.EffectiveStreamId, stream.StreamType, progress, text);
        }
    }

    private void SaveCurrentlyWatchingProgress(int streamId, string streamType, double progress, string progressText)
    {
        try
        {
            var filePath = GetCurrentlyWatchingFilePath();
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StreamItem>>(json) ?? new List<StreamItem>();

                var item = list.FirstOrDefault(x => x.EffectiveStreamId == streamId && x.StreamType == streamType);
                if (item != null)
                {
                    item.WatchProgress = progress;
                    item.WatchProgressText = progressText;

                    var newJson = Newtonsoft.Json.JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(filePath, newJson);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Log("Error saving progress to currently watching", ex);
        }
    }
}
