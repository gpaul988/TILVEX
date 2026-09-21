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

    private void LoginButton_Click(object sender, RoutedEventArgs e)
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
            MessageBox.Show("We could not find a matching account. Check your email and password.", "TILVEX", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        AppSession.CurrentUser = user;
        OpenMainWindow();
    }

    private void SignupButton_Click(object sender, RoutedEventArgs e)
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
