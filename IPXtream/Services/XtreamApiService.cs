using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IPXtream.Models;
using Newtonsoft.Json;

namespace IPXtream.Services;

/// <summary>
/// Handles all HTTP communication with an Xtream Codes API server.
/// Instantiate once and reuse (HttpClient best practice).
/// </summary>
public class XtreamApiService : IDisposable
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly HttpClient _http;
    private readonly HttpClient _downloadHttp; // separate client — no timeout for large files
    private UserCredentials? _credentials;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XtreamApiService()
    {
        // AutomaticDecompression handles gzip/deflate transparently.
        // Without this, manually adding Accept-Encoding causes the server
        // to send compressed bytes that ReadAsStringAsync can't decode.
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                System.Net.DecompressionMethods.GZip |
                System.Net.DecompressionMethods.Deflate
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        _http.DefaultRequestHeaders.UserAgent
             .ParseAdd("IPXtream/1.0 (Windows; WPF)");

        // Separate HttpClient for downloads — user's CancellationToken handles timeouts
        _downloadHttp = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        _downloadHttp.DefaultRequestHeaders.UserAgent.ParseAdd("IPXtream/1.0 (Windows; WPF)");
    }

    // ── Credential management ─────────────────────────────────────────────────

    /// <summary>
    /// Stores credentials in the service so subsequent calls can use them
    /// without passing them every time.
    /// </summary>
    public void SetCredentials(UserCredentials credentials)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));

        // Basic sanity-check: must start with http:// or https://
        if (!credentials.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !credentials.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new XtreamApiException(
                "Server URL must start with http:// or https://  (e.g. http://domain.com:8080)");
        }

        // We do NOT set _http.BaseAddress here because HttpClient throws an
        // InvalidOperationException if BaseAddress is changed after the first request.
        // We will build absolute URLs in BuildApiUrl instead.
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticates the user against the Xtream API.
    /// Returns <see cref="AuthResponse"/> on success, throws on HTTP/parse failure.
    /// </summary>
    public async Task<AuthResponse> AuthenticateAsync(
        UserCredentials credentials,
        CancellationToken ct = default)
    {
        SetCredentials(credentials);

        var url  = BuildApiUrl();
        var json = await GetRawAsync(url, ct);

        if (string.IsNullOrWhiteSpace(json))
            throw new XtreamApiException(
                "Server returned an empty response. Check the server URL, username and password.");

        // Detect HTML error pages (e.g. nginx 404 / wrong port)
        if (json.TrimStart().StartsWith('<'))
            throw new XtreamApiException(
                "Server returned an HTML page instead of JSON. " +
                "Verify the server URL is correct and the service is running.");

        try
        {
            // ── Two-phase parse ───────────────────────────────────────────────
            // Some Xtream servers return  { "user_info": false, "server_info":{} }
            // when credentials are wrong, instead of { "user_info": { "auth": 0 } }.
            // Parse as JObject first so we can inspect the user_info token type.
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);

            UserInfo? userInfo = null;
            var userInfoToken  = root["user_info"];
            if (userInfoToken?.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                userInfo = userInfoToken.ToObject<UserInfo>();
            // If user_info is false/null → userInfo=null → IsAuthenticated=false

            var serverInfo = root["server_info"]?.ToObject<ServerInfo>();

            return new AuthResponse { UserInfo = userInfo, ServerInfo = serverInfo };
        }
        catch (JsonException ex)
        {
            throw new XtreamApiException(
                "Could not parse server response. Verify your server URL, username and password.",
                ex);
        }
    }

    // ── Categories ────────────────────────────────────────────────────────────

    /// <summary>Fetches all Live TV categories.</summary>
    public Task<List<Category>> GetLiveCategoriesAsync(CancellationToken ct = default, bool forceRefresh = false)
        => GetListAsync<Category>(BuildApiUrl("get_live_categories"), ct, forceRefresh);

    /// <summary>Fetches all VOD (Movies) categories.</summary>
    public Task<List<Category>> GetVodCategoriesAsync(CancellationToken ct = default, bool forceRefresh = false)
        => GetListAsync<Category>(BuildApiUrl("get_vod_categories"), ct, forceRefresh);

    /// <summary>Fetches all Series categories.</summary>
    public Task<List<Category>> GetSeriesCategoriesAsync(CancellationToken ct = default, bool forceRefresh = false)
        => GetListAsync<Category>(BuildApiUrl("get_series_categories"), ct, forceRefresh);

    // ── Streams ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches Live streams for the given category.
    /// Pass <c>null</c> or omit <paramref name="categoryId"/> to get ALL live streams.
    /// </summary>
    public Task<List<StreamItem>> GetLiveStreamsAsync(
        string? categoryId = null,
        CancellationToken ct = default,
        bool forceRefresh = false)
    {
        var url = string.IsNullOrWhiteSpace(categoryId)
            ? BuildApiUrl("get_live_streams")
            : BuildApiUrl("get_live_streams", ("category_id", categoryId));

        return GetListAsync<StreamItem>(url, ct, forceRefresh);
    }

    /// <summary>
    /// Fetches VOD streams for the given category.
    /// Pass <c>null</c> to get ALL movies.
    /// </summary>
    public Task<List<StreamItem>> GetVodStreamsAsync(
        string? categoryId = null,
        CancellationToken ct = default,
        bool forceRefresh = false)
    {
        var url = string.IsNullOrWhiteSpace(categoryId)
            ? BuildApiUrl("get_vod_streams")
            : BuildApiUrl("get_vod_streams", ("category_id", categoryId));

        return GetListAsync<StreamItem>(url, ct, forceRefresh);
    }

    /// <summary>
    /// Fetches Series for the given category.
    /// Pass <c>null</c> to get ALL series.
    /// </summary>
    public Task<List<StreamItem>> GetSeriesAsync(
        string? categoryId = null,
        CancellationToken ct = default,
        bool forceRefresh = false)
    {
        var url = string.IsNullOrWhiteSpace(categoryId)
            ? BuildApiUrl("get_series")
            : BuildApiUrl("get_series", ("category_id", categoryId));

        return GetListAsync<StreamItem>(url, ct, forceRefresh);
    }

    /// <summary>
    /// Fetches all seasons/episodes for a given series.
    /// </summary>
    public async Task<SeriesInfoResponse?> GetSeriesInfoAsync(
        int seriesId,
        CancellationToken ct = default,
        bool forceRefresh = false)
    {
        var url = BuildApiUrl("get_series_info", ("series_id", seriesId.ToString()));
        var json = await GetRawAsync(url, ct, forceRefresh);

        try
        {
            return await Task.Run(() => JsonConvert.DeserializeObject<SeriesInfoResponse>(json));
        }
        catch (JsonException ex)
        {
            throw new XtreamApiException(
                $"Failed to parse series info: {ex.Message}", ex);
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Client-side search filter — filters a pre-fetched list by name.
    /// Xtream API has no server-side search endpoint.
    /// </summary>
    public static IEnumerable<StreamItem> Search(
        IEnumerable<StreamItem> items,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return items;

        return items.Where(s =>
            s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    // ── URL Construction helpers ──────────────────────────────────────────────

    /// <summary>
    /// Builds a player_api.php URL with optional action and extra query params.
    /// </summary>
    private string BuildApiUrl(
        string? action = null,
        params (string Key, string Value)[] extraParams)
    {
        EnsureCredentials();

        var sb = new System.Text.StringBuilder();

        // Ensure absolute URL
        sb.Append(_credentials!.BaseUrl.TrimEnd('/'));

        sb.Append("/player_api.php?username=");
        sb.Append(Uri.EscapeDataString(_credentials.Username));
        sb.Append("&password=");
        sb.Append(Uri.EscapeDataString(_credentials.Password));

        if (!string.IsNullOrWhiteSpace(action))
        {
            sb.Append("&action=");
            sb.Append(action);
        }

        foreach (var (key, value) in extraParams)
        {
            sb.Append('&');
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        return sb.ToString();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GetRawAsync(string url, CancellationToken ct, bool forceRefresh = false)
    {
        var cacheFile = GetCacheFilePath(url);

        // 1. Try reading from cache
        if (!forceRefresh && File.Exists(cacheFile))
        {
            try
            {
                return await File.ReadAllTextAsync(cacheFile, ct);
            }
            catch
            {
                // Fall back to network on read error
            }
        }

        // 2. Fetch from network
        try
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);

            // 3. Save to cache in background (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
                    await File.WriteAllTextAsync(cacheFile, json);
                }
                catch { /* Ignore caching errors */ }
            });

            return json;
        }
        catch (HttpRequestException ex)
        {
            throw new XtreamApiException(
                $"Network error while contacting server: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new XtreamApiException(
                "Request timed out. Check your server URL and connection.", ex);
        }
    }

    private async Task<List<T>> GetListAsync<T>(string url, CancellationToken ct, bool forceRefresh = false)
    {
        var json = await GetRawAsync(url, ct, forceRefresh);

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new List<T>();

        try
        {
            return await Task.Run(() => JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>());
        }
        catch (JsonException ex)
        {
            throw new XtreamApiException(
                $"Failed to parse server response: {ex.Message}", ex);
        }
    }

    private void EnsureCredentials()
    {
        if (_credentials is null)
            throw new InvalidOperationException(
                "Call SetCredentials() or AuthenticateAsync() before making API requests.");
    }

    // ── JSON Cache Helpers ────────────────────────────────────────────────────

    private string GetCacheFilePath(string url)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDir = Path.Combine(appData, "IPXtream", "Cache");
        Directory.CreateDirectory(cacheDir);

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
        var filename = string.Concat(hash.Select(b => b.ToString("x2"))) + ".json";
        return Path.Combine(cacheDir, filename);
    }

    // ── Image Cache ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the on-disk path where an image URL is (or will be) cached.
    /// This is public+static so the CachedImageConverter can call it without
    /// holding a reference to the service.
    /// </summary>
    public static string GetImageCachePath(string url)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IPXtream", "ImageCache");
        Directory.CreateDirectory(dir);

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
        var filename = string.Concat(hash.Select(b => b.ToString("x2"))) + ".img";
        return Path.Combine(dir, filename);
    }

    /// <summary>
    /// Downloads one image and saves it to the image cache.
    /// No-ops if already cached or the URL is blank.
    /// </summary>
    public async Task CacheImageAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var path = GetImageCachePath(url);
        if (File.Exists(path)) return;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(path, bytes, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ignore bad/missing thumbnails — don't fail the whole batch
        }
    }

    /// <summary>
    /// Bulk-downloads a collection of image URLs, max 8 concurrent.
    /// Already-cached images are skipped instantly.
    /// Progress reports (done, total) on the caller's SynchronizationContext.
    /// </summary>
    public async Task BulkCacheImagesAsync(
        IEnumerable<string> imageUrls,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var urls = imageUrls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int total = urls.Count;
        int done  = 0;

        await Parallel.ForEachAsync(
            urls,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (url, token) =>
            {
                await CacheImageAsync(url, token);
                progress?.Report((Interlocked.Increment(ref done), total));
            });
    }

    /// <summary>
    /// Bulk-fetches and caches the episode trees for every series ID, max 4 concurrent.
    /// Already-cached JSON is returned from disk without hitting the network.
    /// Progress reports (done, total) on the caller's SynchronizationContext.
    /// </summary>
    public async Task BulkCacheSeriesInfoAsync(
        IEnumerable<int> seriesIds,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var ids = seriesIds.Where(id => id > 0).Distinct().ToList();
        int total = ids.Count;
        int done  = 0;

        await Parallel.ForEachAsync(
            ids,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (id, token) =>
            {
                try   { await GetSeriesInfoAsync(id, token, forceRefresh: false); }
                catch (Exception ex) when (ex is not OperationCanceledException) { /* skip */ }
                finally { progress?.Report((Interlocked.Increment(ref done), total)); }
            });
    }

    // ── Streaming Download ────────────────────────────────────────────────────

    /// <summary>
    /// Downloads a stream URL to <paramref name="destPath"/> with resume support.
    /// Progress reports (downloaded, total, bytesPerSec) on the caller's SynchronizationContext.
    /// </summary>
    public async Task DownloadStreamAsync(
        string url,
        string destPath,
        IProgress<(long downloaded, long total, double bytesPerSec)>? progress = null,
        Func<int>? getSpeedLimitKbps = null,
        CancellationToken ct = default)
    {
        var partPath   = destPath + ".part";
        long resumeFrom = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumeFrom > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);

        using var response = await _downloadHttp.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);

        // 200 = server ignored Range, restart; 206 = partial OK
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
            resumeFrom = 0;

        response.EnsureSuccessStatusCode();

        long serverLen  = response.Content.Headers.ContentLength ?? 0;
        long totalBytes = serverLen + resumeFrom;

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var fileStream = new FileStream(
            partPath,
            resumeFrom > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, 65536, useAsync: true);

        await using var netStream = await response.Content.ReadAsStreamAsync(ct);

        var  buffer     = new byte[81920];
        long downloaded = resumeFrom;
        long speedBytes = 0;
        var  sw         = System.Diagnostics.Stopwatch.StartNew();

        var throttleSw = System.Diagnostics.Stopwatch.StartNew();
        long throttleBytes = 0;

        int read;
        while ((read = await netStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded  += read;
            speedBytes  += read;
            throttleBytes += read;

            // Dynamic Speed Limiting (Throttling)
            int limitKbps = getSpeedLimitKbps?.Invoke() ?? 0;
            if (limitKbps > 0)
            {
                double elapsedMs = throttleSw.Elapsed.TotalMilliseconds;
                double targetMs = (throttleBytes / 1024.0) / limitKbps * 1000.0;
                if (targetMs > elapsedMs)
                {
                    int delayMs = (int)(targetMs - elapsedMs);
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, ct);
                    }
                }

                if (throttleSw.ElapsedMilliseconds >= 1000)
                {
                    throttleBytes = 0;
                    throttleSw.Restart();
                }
            }
            else
            {
                throttleBytes = 0;
                throttleSw.Restart();
            }

            if (sw.ElapsedMilliseconds >= 800)
            {
                double bps = speedBytes / sw.Elapsed.TotalSeconds;
                progress?.Report((downloaded, totalBytes, bps));
                speedBytes = 0;
                sw.Restart();
            }
        }

        progress?.Report((downloaded, totalBytes, 0));

        // Rename .part → final
        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(partPath, destPath);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _http.Dispose();
        _downloadHttp.Dispose();
    }
}

/// <summary>
/// Thrown when the Xtream API call fails for any reason.
/// Wraps HttpRequestException, TaskCanceledException, and JsonException
/// so callers only need one catch block.
/// </summary>
public class XtreamApiException : Exception
{
    public XtreamApiException(string message, Exception? inner = null)
        : base(message, inner) { }
}
