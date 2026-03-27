using System.IO;
using System.Security.Cryptography;
using System.Text;
using IPXtream.Models;
using Newtonsoft.Json;

namespace IPXtream.Helpers;

/// <summary>
/// Persists <see cref="UserCredentials"/> to a local JSON file.
/// The file is encrypted with Windows DPAPI (per-user scope),
/// so it is only readable by the account that saved it.
/// </summary>
public static class CredentialStore
{
    // ── Storage path: %LOCALAPPDATA%\IPXtream\credentials.dat ───────────────

    private static readonly string StorageDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "IPXtream");

    private static readonly string FilePath =
        Path.Combine(StorageDir, "credentials.dat");

    // Optional entropy makes the blob unique to this application
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("IPXtream_SecretEntropy_2024");

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts and saves credentials to disk.
    /// Only called when the user ticks "Remember Me".
    /// </summary>
    public static void Save(UserCredentials credentials)
    {
        Directory.CreateDirectory(StorageDir);

        var json        = JsonConvert.SerializeObject(credentials);
        var plainBytes  = Encoding.UTF8.GetBytes(json);
        var cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(FilePath, cipherBytes);
    }

    /// <summary>
    /// Loads and decrypts credentials from disk.
    /// Returns <c>null</c> when no saved credentials exist or decryption fails.
    /// </summary>
    public static UserCredentials? Load()
    {
        if (!File.Exists(FilePath)) return null;

        try
        {
            var cipherBytes = File.ReadAllBytes(FilePath);
            var plainBytes  = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            var json        = Encoding.UTF8.GetString(plainBytes);

            return JsonConvert.DeserializeObject<UserCredentials>(json);
        }
        catch
        {
            // Corrupted or tampered file — treat as "no saved credentials"
            Clear();
            return null;
        }
    }

    /// <summary>Deletes any saved credential file from disk.</summary>
    public static void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    /// <summary>Returns <c>true</c> if a credential file already exists.</summary>
    public static bool HasSavedCredentials() => File.Exists(FilePath);
}
