using System.Collections.Generic;

namespace IPXtream.Models;

public class SeasonItem
{
    public string SeasonNumber { get; init; } = string.Empty;
    public string DisplayName => $"Season {SeasonNumber}";
    public List<Episode> Episodes { get; init; } = new();
}
