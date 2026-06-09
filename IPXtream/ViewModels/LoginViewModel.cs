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

    // ── Multi-account support properties ──────────────────────────────────────
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<UserCredentials> _savedAccounts = new();

    [ObservableProperty]
    private UserCredentials? _selectedAccount;

    [ObservableProperty]
    private bool _hasSavedAccounts;

    [ObservableProperty]
    private bool _isAutoLoggingIn;

    private AppSettings _settings = new();

    // ── Event: signals the View to navigate to the Dashboard ─────────────────

    public event Action<AuthResponse, UserCredentials>? LoginSucceeded;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LoginViewModel(XtreamApiService api)
    {
        _api = api;
        TryAutoFill();
    }

    // ── Selection changed behavior ────────────────────────────────────────────
    partial void OnSelectedAccountChanged(UserCredentials? value)
    {
        if (value != null)
        {
            ServerUrl  = value.ServerUrl;
            Username   = value.Username;
            Password   = value.Password;
            RememberMe = true;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        IsLoading    = true;

        try
        {
            string rawUrl = ServerUrl.Trim();
            if (rawUrl.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase))
            {
                rawUrl = "http://" + rawUrl.Substring(7);
            }
            else if (rawUrl.StartsWith("HTTPS://", StringComparison.OrdinalIgnoreCase))
            {
                rawUrl = "https://" + rawUrl.Substring(8);
            }

            var creds = new UserCredentials
            {
                ServerUrl  = rawUrl,
                Username   = Username.Trim(),
                Password   = Password,
                RememberMe = RememberMe
            };

            // Validate URL format minimally
            if (!Uri.TryCreate(creds.ServerUrl, UriKind.Absolute, out _))
            {
                ErrorMessage = "Server URL is not valid. Example: http://domain.com:8080";
                IsAutoLoggingIn = false;
                return;
            }

            var auth = await _api.AuthenticateAsync(creds);

            if (!auth.IsAuthenticated)
            {
                ErrorMessage = "Login failed. Check your username, password, or subscription status.";
                IsAutoLoggingIn = false;
                return;
            }

            // Update AppSettings SavedAccounts list
            var existing = _settings.SavedAccounts.FirstOrDefault(a => 
                a.Username.Equals(creds.Username, StringComparison.OrdinalIgnoreCase) && 
                a.ServerUrl.TrimEnd('/').Equals(creds.ServerUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

            if (RememberMe)
            {
                if (existing != null)
                {
                    existing.Password = creds.Password;
                }
                else
                {
                    _settings.SavedAccounts.Add(creds);
                }

                // If no default account is set, set this one
                if (string.IsNullOrEmpty(_settings.DefaultAccountUsername))
                {
                    _settings.DefaultAccountUsername = creds.Username;
                    _settings.DefaultAccountServerUrl = creds.ServerUrl;
                }
            }
            else
            {
                if (existing != null)
                {
                    _settings.SavedAccounts.Remove(existing);
                    if (creds.Username == _settings.DefaultAccountUsername && creds.ServerUrl == _settings.DefaultAccountServerUrl)
                    {
                        _settings.DefaultAccountUsername = null;
                        _settings.DefaultAccountServerUrl = null;
                    }
                }
            }

            CredentialStore.Save(_settings);

            // Clear login bypass for subsequent startups
            App.BypassAutoLogin = false;

            LoginSucceeded?.Invoke(auth, creds);
        }
        catch (XtreamApiException ex)
        {
            ErrorMessage = ex.Message;
            IsAutoLoggingIn = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            IsAutoLoggingIn = false;
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

    // ── Auto-fill and auto-login from saved settings ─────────────────────────

    private void TryAutoFill()
    {
        _settings = CredentialStore.Load();
        if (_settings.SavedAccounts == null || _settings.SavedAccounts.Count == 0)
        {
            HasSavedAccounts = false;
            return;
        }

        SavedAccounts = new System.Collections.ObjectModel.ObservableCollection<UserCredentials>(_settings.SavedAccounts);
        HasSavedAccounts = true;

        // Try to find default account
        var defaultAccount = _settings.SavedAccounts.FirstOrDefault(a => 
            a.Username == _settings.DefaultAccountUsername && 
            a.ServerUrl == _settings.DefaultAccountServerUrl);

        // Fall back to first account if default not set or not found
        if (defaultAccount == null)
        {
            defaultAccount = _settings.SavedAccounts.First();
        }

        SelectedAccount = defaultAccount;

        // Auto-login logic if enabled and not bypassed
        if (_settings.AutoLogin && defaultAccount != null && !App.BypassAutoLogin)
        {
            IsAutoLoggingIn = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(300); // Allow UI to initialize
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (LoginCommand.CanExecute(null))
                    {
                        LoginCommand.Execute(null);
                    }
                    else
                    {
                        IsAutoLoggingIn = false;
                    }
                });
            });
        }
    }
}
