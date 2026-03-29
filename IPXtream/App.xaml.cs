using System.Windows;
using IPXtream.Helpers;
using IPXtream.Services;
using IPXtream.ViewModels;
using IPXtream.Views;

namespace IPXtream;

public partial class App : Application
{
    // ── Shared singleton (created once, reused across all windows) ────────────
    public static readonly XtreamApiService ApiService = new();

    /// <summary>
    /// Set when the user successfully logs in. Used by PlayerWindow to build stream URLs.
    /// </summary>
    public static IPXtream.Models.UserCredentials? CurrentCredentials { get; set; }

    // ── Entry point ───────────────────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Attempt auto-login if credentials were previously saved
        var saved = CredentialStore.Load();

        if (saved is { RememberMe: true })
        {
            // Show the login window with credentials pre-filled;
            // the user can review and press Sign In, or edit if needed.
            ShowLoginWindow(saved);
        }
        else
        {
            ShowLoginWindow(null);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void ShowLoginWindow(IPXtream.Models.UserCredentials? prefill)
    {
        var vm     = new LoginViewModel(ApiService);
        var window = new LoginWindow(vm);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ApiService.Dispose();
        base.OnExit(e);
    }
}
