using System.Windows;
using IPXtream.ViewModels;

namespace IPXtream.Views;

public partial class DashboardWindow : Window
{
    private readonly DashboardViewModel _vm;

    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        _vm.PlayRequested   += OnPlayRequested;
        _vm.LogoutRequested += OnLogoutRequested;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnPlayRequested(Models.StreamItem stream)
    {
        var playerVm = new PlayerViewModel(App.ApiService, stream, _vm);
        var player   = new PlayerWindow(playerVm);
        player.Show();
    }

    private void OnLogoutRequested()
    {
        var login = new LoginWindow(new LoginViewModel(App.ApiService));
        login.Show();
        Close();
    }
}
