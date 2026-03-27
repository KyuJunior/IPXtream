using System.Collections.Generic;
using Newtonsoft.Json;

namespace IPXtream.Models;

/// <summary>
/// Response returned by /player_api.php?action=get_series_info&series_id=...
/// Contains the series metadata and a dictionary of seasons -> episodes.
/// </summary>
public class SeriesInfoResponse
{
    [JsonProperty("info")]
    public SeriesInfo Info { get; set; } = new();

    [JsonProperty("episodes")]
    public Dictionary<string, List<Episode>> Episodes { get; set; } = new();
}

public class SeriesInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("director")]
    public string? Director { get; set; }

    [JsonProperty("cast")]
    public string? Cast { get; set; }

    [JsonProperty("plot")]
    public string? Plot { get; set; }
}

public class Episode
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("container_extension")]
    public string ContainerExtension { get; set; } = "mp4";

    [JsonProperty("custom_sid")]
    public string? CustomSid { get; set; }

    [JsonProperty("info")]
    public EpisodeInfo Info { get; set; } = new();
}

public class EpisodeInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("plot")]
    public string? Plot { get; set; }

    [JsonProperty("duration_secs")]
    public int DurationSecs { get; set; }
}
