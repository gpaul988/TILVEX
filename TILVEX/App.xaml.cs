using System.Windows;

namespace TILVEX;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var store = new PosStore();
        if (!store.Data.Settings.SetupCompleted)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var setupWizard = new SetupWizard(store);
            if (setupWizard.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }

        var authWindow = new AuthWindow();
        authWindow.Show();
    }
}
