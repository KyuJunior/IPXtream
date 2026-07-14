using System.Windows;
using IPXtream.ViewModels;
using IPXtream.Models;

namespace IPXtream.Views;

public partial class LoginWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // Apply saved theme immediately at startup
        try
        {
            var settings = Helpers.CredentialStore.Load();
            Helpers.ThemeHelper.ApplyTheme(settings.SelectedTheme ?? "Dark Purple", this);
        }
        catch { }

        // If credentials were saved, the VM already set the password string —
        // push it into the controls (one-time, at startup only).
        PbPassword.Password = _vm.Password;
        TbPassword.Text = _vm.Password;

        _vm.LoginSucceeded += OnLoginSucceeded;

        // Sync inputs when SelectedAccount changes the Password property in the VM
        _vm.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.Password))
            {
                if (PbPassword.Password != _vm.Password)
                {
                    PbPassword.Password = _vm.Password;
                }
                if (TbPassword.Text != _vm.Password)
                {
                    TbPassword.Text = _vm.Password;
                }
            }
        };
    }

    // ── PasswordBox / TextBox synchronization workaround ───────────────────────
    // WPF's PasswordBox intentionally blocks binding for security reasons.
    // We bridge both the PasswordBox and TextBox manually.
    private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_vm.Password != PbPassword.Password)
        {
            _vm.Password = PbPassword.Password;
            TbPassword.Text = PbPassword.Password;
        }
    }

    private void TbPassword_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_vm.Password != TbPassword.Text)
        {
            _vm.Password = TbPassword.Text;
            PbPassword.Password = TbPassword.Text;
        }
    }

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
