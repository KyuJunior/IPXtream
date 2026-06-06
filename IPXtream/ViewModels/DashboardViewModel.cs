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
public enum MediaSection { LiveTV, VOD, Series, WhatsNew }

/// <summary>
/// ViewModel for DashboardWindow.
/// Drives the sidebar, category list, stream list, and search.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly XtreamApiService _api;
    private DispatcherTimer? _searchTimer;
    private CancellationTokenSource? _cacheCts;
    private readonly SemaphoreSlim _downloadSemaphore = new(2); // max 2 concurrent downloads
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

    // ── Sidebar selection ─────────────────────────────────────────────────────
    [ObservableProperty]
    private MediaSection _activeSection = MediaSection.WhatsNew;

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
    [NotifyPropertyChangedFor(nameof(IsViewingSeriesInfo))]
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
            StatusMessage = string.Empty;
            return;
        }

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
            _allStreams.Add(si);
            Streams.Add(si);
        }

        StatusMessage = $"{value.Episodes.Count} episodes in {value.DisplayName}";
    }

    // ── Search ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;

    private List<StreamItem> _allStreams = new();  // unfiltered master list

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
        if (IsCheckingUpdate) return;
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

    // ── Sidebar navigation commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task SelectSectionAsync(string section)
    {
        ActiveSection     = section switch
        {
            "vod"      => MediaSection.VOD,
            "series"   => MediaSection.Series,
            "whatsnew" => MediaSection.WhatsNew,
            _          => MediaSection.LiveTV
        };

        SelectedCategory = null;
        CurrentSeries    = null;
        Streams.Clear();
        _allStreams.Clear();
        SearchText = string.Empty;

        if (ActiveSection == MediaSection.WhatsNew)
        {
            foreach (var item in _featuredItems)
            {
                item.IsFeatured = true;
                Streams.Add(item);
                _allStreams.Add(item);
            }
            StatusMessage = $"{Streams.Count} featured items";
        }
        else
        {
            await LoadCategoriesAsync();
        }
    }

    [RelayCommand]
    private void Logout()
    {
        Helpers.CredentialStore.Clear();
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
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "IPXtream");
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

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "IPXtream");
        Directory.CreateDirectory(dir);

        var safeName = SanitizeFileName(stream.Name);
        var destPath = Path.Combine(dir, $"{safeName}.{stream.ContainerExtension}");

        var item = new DownloadItem
        {
            Name      = stream.Name,
            Url       = url,
            Extension = stream.ContainerExtension,
            DestPath  = destPath
        };

        Downloads.Add(item);
        ShowDownloadsTray = true;

        _ = RunDownloadTaskAsync(item);
    }

    private async Task RunDownloadTaskAsync(DownloadItem item)
    {
        try
        {
            await _downloadSemaphore.WaitAsync(item.Cts.Token).ConfigureAwait(false);
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
                OnPropertyChanged(nameof(ActiveDownloadCount));
                OnPropertyChanged(nameof(HasActiveDownloads));
            });

            await _api.DownloadStreamAsync(item.Url, item.DestPath, prog, item.Cts.Token);

            item.Status     = DownloadStatus.Completed;
            item.StatusText = "✔ Complete";
            item.Progress   = 1.0;
            item.SpeedText  = string.Empty;
        }
        catch (OperationCanceledException)
        {
            if (item.Status == DownloadStatus.Paused)
            {
                item.StatusText = "॥ Paused";
                item.SpeedText  = string.Empty;
            }
            else
            {
                item.Status     = DownloadStatus.Cancelled;
                item.StatusText = "✕ Cancelled";
                item.SpeedText  = string.Empty;
            }
        }
        catch (Exception ex)
        {
            item.Status     = DownloadStatus.Failed;
            item.StatusText = $"Failed: {ex.Message[..Math.Min(ex.Message.Length, 60)]}";
            item.SpeedText  = string.Empty;
        }
        finally
        {
            _downloadSemaphore.Release();
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
    private void CancelDownload(DownloadItem item)
    {
        item.Status = DownloadStatus.Cancelled;
        item.Cts.Cancel();
        Downloads.Remove(item);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c))
                     .Trim().TrimEnd('.');
    }    [RelayCommand]
    private void ToggleFeatured(StreamItem stream)
    {
        if (stream is null) return;

        stream.IsFeatured = !stream.IsFeatured;
        var key = GetFeaturedKey(stream);

        if (stream.IsFeatured)
        {
            _featuredKeys.Add(key);
            _featuredItems.Add(stream);
        }
        else
        {
            _featuredKeys.Remove(key);
            var existing = _featuredItems.FirstOrDefault(i => GetFeaturedKey(i) == key);
            if (existing != null) _featuredItems.Remove(existing);
        }

        SaveWhatsNew();

        if (ActiveSection == MediaSection.WhatsNew)
        {
            Streams.Remove(stream);
            _allStreams.Remove(stream);
            StatusMessage = $"{Streams.Count} featured items";
        }
    }

    private string GetFeaturedKey(StreamItem stream)
    {
        var id = stream.StreamType == "series" ? stream.SeriesId : stream.EffectiveStreamId;
        return $"{stream.StreamType}_{id}";
    }

    private void MarkFeatured(StreamItem stream)
    {
        stream.IsFeatured = _featuredKeys.Contains(GetFeaturedKey(stream));
    }

    private async Task InitializeWhatsNewAsync()
    {
        await LoadWhatsNewAsync();
        
        if (ActiveSection == MediaSection.WhatsNew)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Streams.Clear();
                _allStreams.Clear();
                foreach (var item in _featuredItems)
                {
                    item.IsFeatured = true;
                    Streams.Add(item);
                    _allStreams.Add(item);
                }
                StatusMessage = $"{Streams.Count} featured items";
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

#if !DEBUG
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
#else
            var path = GetWhatsNewFilePath();
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
#endif

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
        }
        catch { }
    }

#if DEBUG
    public bool IsAdminMode => true;
#else
    public bool IsAdminMode => false;
#endif

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

        // If it's a Series, don't play it — fetch and show its episodes instead.
        if (ActiveSection == MediaSection.Series && CurrentSeries == null)
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
        if (SelectedCategory is not null)
        {
            await LoadStreamsAsync(SelectedCategory.CategoryId);
        }
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
        if (string.IsNullOrWhiteSpace(query) && SelectedCategory?.CategoryId == "")
        {
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
                foreach (var s in filtered)
                {
                    // Abort loop if typing changed
                    if (SearchText != query) return;
                    Streams.Add(s);
                }
            }
        });

        StatusMessage = Streams.Count == 0 && _allStreams.Count > 0
            ? "No results match your search."
            : $"{Streams.Count} / {_allStreams.Count} streams";
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

            Streams.Clear();
            _allStreams = items;

            // If this is the "All" category, drawing thousands of items freezes the app.
            // We store them in _allStreams but leave the UI empty until they search.
            if (string.IsNullOrEmpty(categoryId))
            {
                StatusMessage = $"Loaded {items.Count} items. Type in the search box to find a stream.";
            }
            else
            {
                // Normal category: add items to UI (batched to prevent freezes)
                int count = 0;
                foreach (var s in items)
                {
                    Streams.Add(s);
                    count++;
                    if (count % 100 == 0) await Task.Delay(1); // Yield UI thread
                }

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
}
