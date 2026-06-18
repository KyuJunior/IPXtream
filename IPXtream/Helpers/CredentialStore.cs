using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IPXtream.Models;
using IPXtream.Services;
using Newtonsoft.Json;

namespace IPXtream.Helpers;

/// <summary>
/// Persists <see cref="AppSettings"/> to a local binary file.
/// The file is encrypted with Windows DPAPI (per-user scope),
/// so it is only readable by the account that saved it.
/// </summary>
public static class CredentialStore
{
    private static readonly object FileLock = new();

    private static readonly string StorageDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "IPXtream");

    private static readonly string SettingsFilePath =
        Path.Combine(StorageDir, "settings.dat");

    private static readonly string LegacyCredentialsFilePath =
        Path.Combine(StorageDir, "credentials.dat");

    private static readonly string LegacySettingsJsonPath =
        Path.Combine(StorageDir, "settings.json");

    // Optional entropy makes the blob unique to this application
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("IPXtream_SecretEntropy_2024");

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts and saves settings to disk.
    /// </summary>
    public static void Save(AppSettings settings)
    {
        lock (FileLock)
        {
            try
            {
                Directory.CreateDirectory(StorageDir);

                var json        = JsonConvert.SerializeObject(settings, Formatting.Indented);
                var plainBytes  = Encoding.UTF8.GetBytes(json);
                var cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);

                var tempPath = SettingsFilePath + ".tmp";
                File.WriteAllBytes(tempPath, cipherBytes);
                if (File.Exists(SettingsFilePath))
                {
                    File.Delete(SettingsFilePath);
                }
                File.Move(tempPath, SettingsFilePath);
            }
            catch (Exception ex)
            {
                LogService.Log("Failed to save credentials/settings to disk.", ex);
            }
        }
    }

    /// <summary>
    /// Loads and decrypts settings from disk, migrating any legacy files.
    /// </summary>
    public static AppSettings Load()
    {
        lock (FileLock)
        {
            var settings = new AppSettings();

            // 1. Load settings.dat if it exists
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var cipherBytes = File.ReadAllBytes(SettingsFilePath);
                    var plainBytes  = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                    var json        = Encoding.UTF8.GetString(plainBytes);
                    var loaded      = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (loaded != null)
                    {
                        settings = loaded;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log("Corrupted settings.dat or DPAPI decryption failed. Using default settings.", ex);
                }
            }

            bool needsSave = false;

            // 2. Migrate legacy single credentials file (credentials.dat)
            if (File.Exists(LegacyCredentialsFilePath))
            {
                try
                {
                    var cipherBytes = File.ReadAllBytes(LegacyCredentialsFilePath);
                    var plainBytes  = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                    var json        = Encoding.UTF8.GetString(plainBytes);
                    var legacyCreds = JsonConvert.DeserializeObject<UserCredentials>(json);

                    if (legacyCreds != null)
                    {
                        // Add legacy creds to the list if not already present
                        if (!settings.SavedAccounts.Any(a => a.Username == legacyCreds.Username && a.ServerUrl == legacyCreds.ServerUrl))
                        {
                            settings.SavedAccounts.Add(legacyCreds);
                        }
                        // Make it default
                        settings.DefaultAccountUsername = legacyCreds.Username;
                        settings.DefaultAccountServerUrl = legacyCreds.ServerUrl;
                        needsSave = true;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log("Failed to migrate legacy credentials file.", ex);
                }
                finally
                {
                    try { File.Delete(LegacyCredentialsFilePath); } catch (Exception ex) { LogService.Log("Failed to delete legacy credentials.dat file.", ex); }
                }
            }

            // 3. Migrate legacy settings file (settings.json)
            if (File.Exists(LegacySettingsJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(LegacySettingsJsonPath);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (dict != null && dict.TryGetValue("DownloadFolder", out var path) && !string.IsNullOrWhiteSpace(path))
                    {
                        settings.DownloadFolder = path;
                        needsSave = true;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log("Failed to migrate legacy settings file.", ex);
                }
                finally
                {
                    try { File.Delete(LegacySettingsJsonPath); } catch (Exception ex) { LogService.Log("Failed to delete legacy settings.json file.", ex); }
                }
            }

            // Apply default download folder if empty
            if (string.IsNullOrWhiteSpace(settings.DownloadFolder))
            {
                settings.DownloadFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "IPXtream");
                needsSave = true;
            }

            if (needsSave)
            {
                Save(settings);
            }

            return settings;
        }
    }

    /// <summary>Deletes any saved settings file from disk.</summary>
    public static void Clear()
    {
        lock (FileLock)
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                    File.Delete(SettingsFilePath);
            }
            catch (Exception ex)
            {
                LogService.Log("Failed to clear/delete settings file.", ex);
            }
        }
    }

    /// <summary>Returns <c>true</c> if a settings file already exists.</summary>
    public static bool HasSavedSettings()
    {
        lock (FileLock)
        {
            return File.Exists(SettingsFilePath);
        }
    }
}
