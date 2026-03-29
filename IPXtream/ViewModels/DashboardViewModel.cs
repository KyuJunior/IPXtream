using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPXtream.Models;
using IPXtream.Services;

namespace IPXtream.ViewModels;

/// <summary>
/// Sidebar section identifiers.
/// </summary>
public enum MediaSection { LiveTV, VOD, Series }

/// <summary>
/// ViewModel for DashboardWindow.
/// Drives the sidebar, category list, stream list, and search.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly XtreamApiService _api;
    private DispatcherTimer? _searchTimer;

    // ── Auth info (displayed in header) ───────────────────────────────────────
    public string DisplayUsername { get; }
    public string ServerDisplay   { get; }

    // ── Sidebar selection ─────────────────────────────────────────────────────
    [ObservableProperty]
    private MediaSection _activeSection = MediaSection.LiveTV;

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

    // ── Constructor ───────────────────────────────────────────────────────────
    public DashboardViewModel(
        XtreamApiService api,
        UserCredentials creds,
        AuthResponse auth)
    {
        _api           = api;
        DisplayUsername = creds.Username;
        ServerDisplay   = new Uri(creds.BaseUrl).Host;

        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Streams, _streamsLock);
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Categories, _categoriesLock);

        // Load Live TV categories on startup
        _ = LoadCategoriesAsync();
    }

    // ── Sidebar navigation commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task SelectSectionAsync(string section)
    {
        ActiveSection     = section switch
        {
            "vod"    => MediaSection.VOD,
            "series" => MediaSection.Series,
            _        => MediaSection.LiveTV
        };

        SelectedCategory = null;
        CurrentSeries    = null;
        Streams.Clear();
        _allStreams.Clear();
        SearchText = string.Empty;

        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private void Logout()
    {
        Helpers.CredentialStore.Clear();
        LogoutRequested?.Invoke();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
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
        StatusMessage = "Caching all libraries in background...";

        try
        {
            // Fire and forget caching on a background thread
            _ = Task.Run(async () =>
            {
                try
                {
                    // Live TV
                    App.Current.Dispatcher.Invoke(() => StatusMessage = "Caching Live TV... (1/3)");
                    await _api.GetLiveStreamsAsync(null, default, forceRefresh: true);

                    // Movies
                    App.Current.Dispatcher.Invoke(() => StatusMessage = "Caching Movies... (2/3)");
                    await _api.GetVodStreamsAsync(null, default, forceRefresh: true);

                    // Series
                    App.Current.Dispatcher.Invoke(() => StatusMessage = "Caching Series... (3/3)");
                    await _api.GetSeriesAsync(null, default, forceRefresh: true);

                    App.Current.Dispatcher.Invoke(() => StatusMessage = "Background caching complete! 'All' searches will now be instant.");
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() => StatusMessage = $"Caching failed: {ex.Message}");
                }
                finally
                {
                    App.Current.Dispatcher.Invoke(() => IsCachingAll = false);
                }
            });
        }
        catch { } // Ensure command itself doesn't crash on start
    }

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
        IsLoadingStreams = true;
        ErrorMessage    = string.Empty;
        Streams.Clear();
        _allStreams.Clear();
        SearchText = string.Empty;

        try
        {
            var items = ActiveSection switch
            {
                MediaSection.VOD    => await _api.GetVodStreamsAsync(categoryId, forceRefresh: forceRefresh),
                MediaSection.Series => await _api.GetSeriesAsync(categoryId, forceRefresh: forceRefresh),
                _                   => await _api.GetLiveStreamsAsync(categoryId, forceRefresh: forceRefresh)
            };

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

        try
        {
            var info = await _api.GetSeriesInfoAsync(series.SeriesId, forceRefresh: forceRefresh);
            if (info is null) return;

            CurrentSeries = info;

            // Build the list on a background thread to keep UI somewhat responsive
            var mappedEpisodes = await Task.Run(() =>
            {
                var list = new List<StreamItem>();
                foreach (var seasonObj in info.Episodes.Values)
                {
                    foreach (var ep in seasonObj)
                    {
                        _ = int.TryParse(ep.Id, out int epId);
                        list.Add(new StreamItem
                        {
                            Name               = string.IsNullOrWhiteSpace(ep.Title) ? ep.Info.Name : ep.Title,
                            StreamType         = "series",
                            StreamId           = epId,
                            VideoId            = epId,
                            ContainerExtension = ep.ContainerExtension ?? "mp4",
                            Plot               = ep.Info.Plot,
                            CustomSid          = ep.CustomSid
                        });
                    }
                }
                return list;
            });

            _allStreams = mappedEpisodes;
            foreach (var s in mappedEpisodes)
                Streams.Add(s);

            StatusMessage = $"{mappedEpisodes.Count} episodes";
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
