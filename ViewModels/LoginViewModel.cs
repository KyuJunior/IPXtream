using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPXtream.Helpers;
using IPXtream.Models;
using IPXtream.Services;

namespace IPXtream.ViewModels;

/// <summary>
/// ViewModel for <see cref="Views.LoginWindow"/>.
/// Uses CommunityToolkit.Mvvm source generators for boilerplate-free
/// ObservableProperty and RelayCommand.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly XtreamApiService _api;

    // ── Observable properties ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = string.Empty;

    // Password is bound via code-behind (PasswordBox doesn't support binding)
    // The View sets this property directly.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    // ── Event: signals the View to navigate to the Dashboard ─────────────────

    public event Action<AuthResponse, UserCredentials>? LoginSucceeded;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LoginViewModel(XtreamApiService api)
    {
        _api = api;
        TryAutoFill();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        IsLoading    = true;

        try
        {
            var creds = new UserCredentials
            {
                ServerUrl  = ServerUrl.Trim(),
                Username   = Username.Trim(),
                Password   = Password,
                RememberMe = RememberMe
            };

            // Validate URL format minimally
            if (!Uri.TryCreate(creds.ServerUrl, UriKind.Absolute, out _))
            {
                ErrorMessage = "Server URL is not valid. Example: http://domain.com:8080";
                return;
            }

            var auth = await _api.AuthenticateAsync(creds);

            if (!auth.IsAuthenticated)
            {
                ErrorMessage = "Login failed. Check your username, password, or subscription status.";
                return;
            }

            // Persist credentials if requested
            if (RememberMe)
                CredentialStore.Save(creds);
            else
                CredentialStore.Clear();

            LoginSucceeded?.Invoke(auth, creds);
        }
        catch (XtreamApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLogin() =>
        !string.IsNullOrWhiteSpace(ServerUrl) &&
        !string.IsNullOrWhiteSpace(Username)  &&
        !string.IsNullOrWhiteSpace(Password);

    // ── Auto-fill from saved credentials ─────────────────────────────────────

    private void TryAutoFill()
    {
        var saved = CredentialStore.Load();
        if (saved is null) return;

        ServerUrl  = saved.ServerUrl;
        Username   = saved.Username;
        Password   = saved.Password;
        RememberMe = true;          // checkbox reflects that creds were saved
    }
}
