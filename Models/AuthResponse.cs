using Newtonsoft.Json;

namespace IPXtream.Models;

// ── Auth response ─────────────────────────────────────────────────────────────

/// <summary>
/// Top-level response returned by /player_api.php?username=&amp;password=
/// Contains both the user_info and server_info blocks.
/// </summary>
public class AuthResponse
{
    [JsonProperty("user_info")]
    public UserInfo? UserInfo { get; set; }

    [JsonProperty("server_info")]
    public ServerInfo? ServerInfo { get; set; }

    /// <summary>True when auth succeeded and the account is active.</summary>
    [JsonIgnore]
    public bool IsAuthenticated =>
        UserInfo is { Auth: 1, Status: "Active" };
}

public class UserInfo
{
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>1 = authenticated, 0 = failed.</summary>
    [JsonProperty("auth")]
    public int Auth { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("exp_date")]
    public string? ExpDate { get; set; }

    [JsonProperty("is_trial")]
    public string? IsTrial { get; set; }

    [JsonProperty("active_cons")]
    public string? ActiveCons { get; set; }

    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    [JsonProperty("max_connections")]
    public string? MaxConnections { get; set; }

    [JsonProperty("allowed_output_formats")]
    public List<string> AllowedOutputFormats { get; set; } = new();
}

public class ServerInfo
{
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("port")]
    public string Port { get; set; } = string.Empty;

    [JsonProperty("https_port")]
    public string HttpsPort { get; set; } = string.Empty;

    [JsonProperty("server_protocol")]
    public string ServerProtocol { get; set; } = string.Empty;

    [JsonProperty("rtmp_port")]
    public string RtmpPort { get; set; } = string.Empty;

    [JsonProperty("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonProperty("timestamp_now")]
    public long TimestampNow { get; set; }

    [JsonProperty("time_now")]
    public string TimeNow { get; set; } = string.Empty;
}
