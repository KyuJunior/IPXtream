using System.Windows;
using IPXtream.ViewModels;
using IPXtream.Models;

namespace IPXtream.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // If credentials were saved, the VM already set the password string —
        // push it into the PasswordBox (one-time, at startup only).
        PbPassword.Password = _vm.Password;

        _vm.LoginSucceeded += OnLoginSucceeded;
    }

    // ── PasswordBox workaround ────────────────────────────────────────────────
    // WPF's PasswordBox intentionally blocks binding for security reasons.
    // We bridge it manually via the PasswordChanged event.
    private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Password = PbPassword.Password;

    // ── Navigation ────────────────────────────────────────────────────────────
    private void OnLoginSucceeded(AuthResponse auth, UserCredentials creds)
    {
        // Store credentials globally so PlayerWindow can build stream URLs
        App.CurrentCredentials = creds;

        var dashboard = new DashboardWindow(
            new DashboardViewModel(App.ApiService, creds, auth));

        dashboard.Show();
        Close();
    }
}
