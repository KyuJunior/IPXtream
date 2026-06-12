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
public enum MediaSection { LiveTV, VOD, Series, WhatsNew, Downloads, Settings }

/// <summary>
/// ViewModel for DashboardWindow.
/// Drives the sidebar, category list, stream list, and search.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly XtreamApiService _api;
    private DispatcherTimer? _searchTimer;
    private CancellationTokenSource? _cacheCts;
    private SemaphoreSlim _downloadSemaphore = new(2); // max concurrent downloads
    private readonly List<StreamItem> _featuredItems = new();
    private readonly HashSet<string> _featuredKeys = new();

    // ── Auth info (displayed in header) ───────────────────────────────────────
    public string DisplayUsername { get; }
    public string ServerDisplay   { get; }

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

    // ── Sidebar selection ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeroBanner))]
    private MediaSection _activeSection = MediaSection.WhatsNew;

    // ── Carousel (for What's New Hero) ────────────────────────────────────────
    public ObservableCollection<StreamItem> FeaturedItems { get; } = new();
    public ObservableCollection<StreamItem> FeaturedLive { get; } = new();
    public ObservableCollection<StreamItem> FeaturedShows { get; } = new();
    public ObservableCollection<StreamItem> FeaturedMovies { get; } = new();
    public ObservableCollection<StreamItem> DevRecommendations { get; } = new();

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
        _ = LoadStreamsAsync(value.CategoryId);
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
                ContainerExtension = ep.ContainerExtension ?? "mp4",
                Plot               = ep.Info.Plot,
                CustomSid          = ep.CustomSid,
                StreamIcon         = !string.IsNullOrEmpty(ep.Info.MovieImage) ? ep.Info.MovieImage : (CurrentSeries?.Info.Cover ?? string.Empty)
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
    public event Action<StreamItem>? PlayRequested;

    // ── Embedded player VM (null when no stream is playing) ───────────────────
    [ObservableProperty]
    private PlayerViewModel? _playerVm;

    // ── Event: Logout ─────────────────────────────────────────────────────────
    public event Action? LogoutRequested;

    // ── Downloads ─────────────────────────────────────────────────────────────
    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    [ObservableProperty] private bool _showDownloadsTray;
    [ObservableProperty] private string _downloadFolder = string.Empty;

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
        catch
        {
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
        ActiveSection     = section switch
        {
            "vod"       => MediaSection.VOD,
            "series"    => MediaSection.Series,
            "whatsnew"  => MediaSection.WhatsNew,
            "downloads" => MediaSection.Downloads,
            "settings"  => MediaSection.Settings,
            _           => MediaSection.LiveTV
        };

        SelectedCategory = null;
        CurrentSeries    = null;
        SelectedMovieForInfo = null;
        Streams.Clear();
        _allStreams.Clear();
        SearchText = string.Empty;

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
    private void Logout()
    {
        SelectedMovieForInfo = null;
        LogoutRequested?.Invoke();
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
    }

    private async Task InitializeWhatsNewAsync()
    {
        await LoadWhatsNewAsync();
        
        SyncFeaturedItemsCollection();
        if (_featuredItems.Count > 0)
        {
            _carouselIndex = 0;
            FeaturedCarouselItem = _featuredItems[0];
            Application.Current?.Dispatcher.BeginInvoke(() => StartCarouselTimer());
        }

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
        PlayRequested?.Invoke(stream);
    }

    [RelayCommand]
    private async Task BackToSeriesAsync()
    {
        CurrentSeries = null;
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
                _searchTimer.Stop();
                await PerformSearchAsync(SearchText);
            };
        }

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private async Task PerformSearchAsync(string query)
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
        
        // Background batch rendering using synchronized locks (10x faster)
        await Task.Run(() =>
        {
            lock (_streamsLock)
            {
                // Abort if typing changed
                if (SearchText != query) return;
            }
        });

        if (SearchText == query)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                SetFilteredStreams(filtered);
                StatusMessage = filtered.Count == 0 && _allStreams.Count > 0
                    ? "No results match your search."
                    : $"{filtered.Count} / {_allStreams.Count} streams";
            });
        }
    }

    // ── Private loaders ───────────────────────────────────────────────────────

    private async Task LoadCategoriesAsync(bool forceRefresh = false)
    {
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

    private async Task LoadStreamsAsync(string categoryId, bool forceRefresh = false)
    {
        if (ActiveSection == MediaSection.WhatsNew) return;
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
                MediaSection.VOD    => await _api.GetVodStreamsAsync(categoryId, forceRefresh: forceRefresh),
                MediaSection.Series => await _api.GetSeriesAsync(categoryId, forceRefresh: forceRefresh),
                _                   => await _api.GetLiveStreamsAsync(categoryId, forceRefresh: forceRefresh)
            };

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
        catch (XtreamApiException ex)
        {
            ErrorMessage  = ex.Message;
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
    private void ApplyGeneralSettings()
    {
        SaveSettings();
    }
}
