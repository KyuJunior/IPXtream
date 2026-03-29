using Newtonsoft.Json;

namespace IPXtream.Models;

/// <summary>
/// Represents a stream category (Live TV / VOD / Series).
/// Maps to the JSON returned by get_live_categories / get_vod_categories / get_series_categories.
/// </summary>
public class Category
{
    [JsonProperty("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonProperty("category_name")]
    public string CategoryName { get; set; } = string.Empty;

    [JsonProperty("parent_id")]
    public int ParentId { get; set; }

    // Convenience display alias
    public override string ToString() => CategoryName;
}
