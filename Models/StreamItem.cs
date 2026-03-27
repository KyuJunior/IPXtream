using Newtonsoft.Json;

namespace IPXtream.Models;

/// <summary>
/// Represents an individual stream (live channel, movie, or series).
/// The same model covers live streams and VOD; use the StreamType discriminator
/// to tell them apart when building the playback URL.
/// </summary>
public class StreamItem
{
    // ── Common ──────────────────────────────────────────────────────────────

    [JsonProperty("num")]
    public int Num { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("stream_icon")]
    public string StreamIcon { get; set; } = string.Empty;

    [JsonProperty("epg_channel_id")]
    public string? EpgChannelId { get; set; }

    [JsonProperty("added")]
    public string? Added { get; set; }

    [JsonProperty("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonProperty("custom_sid")]
    public string? CustomSid { get; set; }

    [JsonProperty("tv_archive")]
    public int TvArchive { get; set; }

    [JsonProperty("direct_source")]
    public string? DirectSource { get; set; }

    [JsonProperty("tv_archive_duration")]
    public int TvArchiveDuration { get; set; }

    // ── Live TV ─────────────────────────────────────────────────────────────

    [JsonProperty("stream_id")]
    public int StreamId { get; set; }

    /// <summary>
    /// Extension used when building live stream URLs (usually "ts" or "m3u8").
    /// </summary>
    [JsonProperty("container_extension")]
    public string ContainerExtension { get; set; } = "ts";

    [JsonProperty("stream_type")]
    public string StreamType { get; set; } = string.Empty;

    [JsonProperty("rating")]
    public string? Rating { get; set; }

    [JsonProperty("rating_5based")]
    public double Rating5Based { get; set; }

    [JsonProperty("thumbnail")]
    public string? Thumbnail { get; set; }

    // ── VOD / Series ────────────────────────────────────────────────────────

    /// <summary>VOD only – reuses stream_id field above (same JSON key).</summary>
    [JsonProperty("video_id")]         // some providers use this
    public int VideoId { get; set; }

    /// <summary>Series only - the ID required to get episodes.</summary>
    [JsonProperty("series_id")]
    public int SeriesId { get; set; }

    [JsonProperty("plot")]
    public string? Plot { get; set; }

    [JsonProperty("cast")]
    public string? Cast { get; set; }

    [JsonProperty("director")]
    public string? Director { get; set; }

    [JsonProperty("genre")]
    public string? Genre { get; set; }

    [JsonProperty("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonProperty("last_modified")]
    public string? LastModified { get; set; }

    [JsonProperty("youtube_trailer")]
    public string? YoutubeTrailer { get; set; }

    // ── Display helpers ──────────────────────────────────────────────────────

    public override string ToString() => Name;

    /// <summary>Effective stream ID, works for both live and VOD.</summary>
    public int EffectiveStreamId => StreamId != 0 ? StreamId : VideoId;
}
