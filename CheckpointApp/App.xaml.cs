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

            // --- КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ ---
            // Мы явно указываем приложению, что оно должно закрываться только
            // по команде Shutdown(), а не после закрытия последнего окна.
            // Это предотвратит преждевременное завершение работы.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var userCount = await _databaseService.GetUserCountAsync();

            if (userCount == 0)
            {
                var firstAdminWindow = new FirstAdminWindow
                {
                    DataContext = new FirstAdminViewModel(_databaseService)
                };

                // Если пользователь закрывает окно создания админа, мы явно завершаем приложение.
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

                var mainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(_databaseService, loginViewModel.LoggedInUser!)
                };

                // --- ВТОРОЕ КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ ---
                // Теперь, когда у нас есть настоящее главное окно, мы возвращаем
                // стандартный режим завершения работы. Приложение будет работать,
                // пока пользователь не закроет это главное окно.
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                // Если пользователь закрывает окно входа, мы явно завершаем приложение.
                Shutdown();
            }
        }
    }
}
