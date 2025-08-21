using System.Linq;
using System.Windows;
using CheckpointApp.DataAccess;
using CheckpointApp.ViewModels;
using CheckpointApp.Views;

namespace CheckpointApp
{
    public partial class App : Application
    {
        private readonly DatabaseService _databaseService;

        public App()
        {
            _databaseService = new DatabaseService();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var userCount = await _databaseService.GetUserCountAsync();

            if (userCount == 0)
            {
                var firstAdminWindow = new FirstAdminWindow
                {
                    DataContext = new FirstAdminViewModel(_databaseService)
                };

                if (firstAdminWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            ShowLoginWindow();
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow
            {
                DataContext = new LoginViewModel(_databaseService)
            };

            if (loginWindow.ShowDialog() == true)
            {
                var loginViewModel = (LoginViewModel)loginWindow.DataContext;

                // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
                // Мы явно указываем, что нужно создать экземпляр MainWindow
                // из пространства имен (и папки) Views.
                var mainWindow = new Views.MainWindow
                {
                    DataContext = new MainViewModel(_databaseService, loginViewModel.LoggedInUser!)
                };

                Current.MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
