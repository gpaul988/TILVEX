using System.Windows;

namespace TILVEX;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var authWindow = new AuthWindow();
        authWindow.Show();
    }
}
