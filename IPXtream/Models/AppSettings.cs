using System.Collections.Generic;

namespace IPXtream.Models;

/// <summary>
/// Persists app preferences and saved credentials list in an encrypted file.
/// </summary>
public class AppSettings
{
    public List<UserCredentials> SavedAccounts { get; set; } = new();
    public string? DefaultAccountUsername { get; set; }
    public string? DefaultAccountServerUrl { get; set; }
    
    // Preferences
    public bool AutoLogin { get; set; } = true;
    public int MaxConcurrentDownloads { get; set; } = 2;
    public string DefaultContainerExtension { get; set; } = "ts";
    public string DownloadFolder { get; set; } = string.Empty;
    public string? GithubToken { get; set; }
    public string SelectedPlayerEngine { get; set; } = "Flyleaf";
    public string SelectedTheme { get; set; } = "Dark Purple";
}
