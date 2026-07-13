using SABPlus.Radial.Setup.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace SABPlus.Radial.Setup
{
    public partial class MainWindow : Window
    {
        private readonly InstallerService _installerService;

        public MainWindow()
        {
            InitializeComponent();
            _installerService = new InstallerService();
            InstallPathText.Text = _installerService.InstallDirectory;
            UninstallButton.IsEnabled = _installerService.IsInstalled;
        }

        public void BeginUninstallFromCommandLine()
        {
            Dispatcher.BeginInvoke(new Action(async () => await UninstallAsync(false)));
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (Revit2023CheckBox.IsChecked != true && Revit2024CheckBox.IsChecked != true)
            {
                MessageBox.Show(this, "Выберите хотя бы одну версию Revit.", "SAB+", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "Установка компонентов SAB+…");
            try
            {
                await Task.Run(() => _installerService.Install(
                    Revit2023CheckBox.IsChecked == true,
                    Revit2024CheckBox.IsChecked == true));
                StatusText.Text = "Установка завершена. Перезапустите Revit, если он был открыт.";
                UninstallButton.IsEnabled = true;

                if (LaunchOverlayCheckBox.IsChecked == true)
                {
                    _installerService.LaunchOverlay();
                }
            }
            catch (Exception exception)
            {
                StatusText.Text = "Установка не завершена.";
                MessageBox.Show(this, exception.ToString(), "Ошибка установки SAB+", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false, StatusText.Text);
            }
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            await UninstallAsync(true);
        }

        private async Task UninstallAsync(bool askConfirmation)
        {
            if (askConfirmation)
            {
                MessageBoxResult result = MessageBox.Show(
                    this,
                    "Удалить SAB+ Радиальное колесо и подключения Revit 2023–2024? Пользовательские настройки колеса будут сохранены.",
                    "Удаление SAB+",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            SetBusy(true, "Подготовка удаления…");
            try
            {
                await Task.Run(() => _installerService.BeginSelfUninstall());
                Close();
            }
            catch (Exception exception)
            {
                SetBusy(false, "Удаление не запущено.");
                MessageBox.Show(this, exception.ToString(), "Ошибка удаления SAB+", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetBusy(bool isBusy, string status)
        {
            InstallButton.IsEnabled = !isBusy;
            UninstallButton.IsEnabled = !isBusy && _installerService.IsInstalled;
            ProgressBar.IsIndeterminate = isBusy;
            StatusText.Text = status;
        }
    }
}
