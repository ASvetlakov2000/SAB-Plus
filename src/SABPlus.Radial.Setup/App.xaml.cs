using SABPlus.Radial.Setup.Services;
using System;
using System.Linq;
using System.Windows;

namespace SABPlus.Radial.Setup
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string[] arguments = e.Args ?? new string[0];
            if (arguments.Length >= 3 &&
                string.Equals(arguments[0], "--remove-installation", StringComparison.OrdinalIgnoreCase))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int processId;
                int.TryParse(arguments[2], out processId);
                try
                {
                    InstallerService.RemoveInstallation(arguments[1], processId);
                    Shutdown(0);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, "SAB+ — удаление", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                }

                return;
            }

            base.OnStartup(e);
            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Show();

            if (arguments.Any(item => string.Equals(item, "--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                window.BeginUninstallFromCommandLine();
            }
        }
    }
}
