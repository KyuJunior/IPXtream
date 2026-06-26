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

    /// <summary>
    /// Set to true when logging out to prevent immediate auto-login loop.
    /// </summary>
    public static bool BypassAutoLogin { get; set; }

    // ── Entry point ───────────────────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply saved theme at startup
        try
        {
            var settings = CredentialStore.Load();
            ThemeHelper.ApplyTheme(settings.SelectedTheme ?? "Dark Purple");
        }
        catch { }

        System.AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            LogService.Log("Unhandled AppDomain Exception", ev.ExceptionObject as Exception);
        };

        this.DispatcherUnhandledException += (s, ev) =>
        {
            LogService.Log("Unhandled Dispatcher Exception", ev.Exception);
            ev.Handled = true;
        };

        ShowLoginWindow();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void ShowLoginWindow()
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
