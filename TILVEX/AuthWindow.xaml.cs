using System.Windows;

namespace TILVEX;

public partial class AuthWindow : Window
{
    private readonly PosStore _store = new();

    public AuthWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoginEmail.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var email = LoginEmail.Text.Trim();
        var password = LoginPassword.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Please enter both email and password.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = _store.Authenticate(email, password);
        if (user is null)
        {
            var remote = await new SharedProfileService(_store.Data.Settings.Integrations).LoginAsync(email, password);
            if (remote.Account is null || remote.Business is null)
            {
                MessageBox.Show("We could not find a matching account. Check your email and password.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _store.ImportSharedProfile(remote.Account, remote.Business);
            user = remote.Account;
        }

        var sync = await new SharedProfileService(_store.Data.Settings.Integrations).SyncAsync(user, _store.Data.Settings, password, "LOGIN");
        if (!sync.Success && sync.Status == IntegrationConnectionStatus.Offline)
            sync = await new SharedProfileService(_store.Data.Settings.Integrations).SyncAsync(user, _store.Data.Settings, password, "REGISTER");
        if (!sync.Success && sync.Status == IntegrationConnectionStatus.Error)
        {
            MessageBox.Show(sync.Message, "TILVEX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppSession.CurrentUser = user;
        OpenMainWindow();
    }

    private async void SignupButton_Click(object sender, RoutedEventArgs e)
    {
        var name = SignupName.Text.Trim();
        var email = SignupEmail.Text.Trim();
        var password = SignupPassword.Password;
        var confirmPassword = SignupConfirmPassword.Password;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Please complete all sign-up fields.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!password.Equals(confirmPassword, System.StringComparison.Ordinal))
        {
            MessageBox.Show("The passwords do not match.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (password.Length < 6)
        {
            MessageBox.Show("Your password must be at least 6 characters long.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pendingAccount = new UserAccount { UserName = name, Email = email };
        var profileSync = await new SharedProfileService(_store.Data.Settings.Integrations)
            .SyncAsync(pendingAccount, _store.Data.Settings, password, "REGISTER");
        if (!profileSync.Success && profileSync.Status == IntegrationConnectionStatus.Error)
        {
            MessageBox.Show(profileSync.Message, "TILVEX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var account = _store.Register(name, email, password);
        if (account is null)
        {
            MessageBox.Show("An account with that email already exists.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AppSession.CurrentUser = account;
        MessageBox.Show($"Welcome {account.UserName}! Your TILVEX account is ready.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Information);
        OpenMainWindow();
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }
}
