namespace IPXtream.Models;

/// <summary>
/// Stores the credentials the user enters on the Login screen.
/// This object is also persisted locally (encrypted) when "Remember Me" is checked.
/// </summary>
public class UserCredentials
{
    public string ServerUrl { get; set; } = string.Empty;
    public string Username  { get; set; } = string.Empty;
    public string Password  { get; set; } = string.Empty;

    /// <summary>Indicates the user wants credentials saved between sessions.</summary>
    public bool RememberMe { get; set; }

    // ── Derived helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Base URL normalised so it never ends with a slash.
    /// e.g. "http://demo.server.com:8080"
    /// </summary>
    public string BaseUrl =>
        string.IsNullOrWhiteSpace(ServerUrl)
            ? string.Empty
            : ServerUrl.TrimEnd('/');

    /// <summary>Builds the standard player_api.php authentication URL.</summary>
    public string AuthUrl =>
        $"{BaseUrl}/player_api.php?username={Uri.EscapeDataString(Username)}&password={Uri.EscapeDataString(Password)}";

    /// <summary>
    /// Builds a live-stream playback URL.
    /// e.g. http://server:port/live/user/pass/12345.ts
    /// </summary>
    public string BuildLiveStreamUrl(int streamId, string extension = "ts") =>
        $"{BaseUrl}/live/{Uri.EscapeDataString(Username)}/{Uri.EscapeDataString(Password)}/{streamId}.{extension}";

    /// <summary>
    /// Builds a VOD playback URL.
    /// e.g. http://server:port/movie/user/pass/12345.mp4
    /// </summary>
    public string BuildVodUrl(int streamId, string extension = "mp4") =>
        $"{BaseUrl}/movie/{Uri.EscapeDataString(Username)}/{Uri.EscapeDataString(Password)}/{streamId}.{extension}";

    public override string ToString() => $"{Username}@{BaseUrl}";
}
