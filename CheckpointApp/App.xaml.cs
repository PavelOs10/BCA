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

            // --- ИСПРАВЛЕНИЕ ЖИЗНЕННОГО ЦИКЛА 1 ---
            // Мы явно указываем приложению, что оно должно закрываться только
            // по команде Shutdown(), а не после закрытия последнего окна.
            // Это предотвратит преждевременное завершение работы после закрытия
            // окна создания администратора.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var userCount = await _databaseService.GetUserCountAsync();

            if (userCount == 0)
            {
                var firstAdminWindow = new FirstAdminWindow
                {
                    DataContext = new FirstAdminViewModel(_databaseService)
                };

                // Теперь мы проверяем результат диалога. ViewModel установит DialogResult в true
                // только при успешном создании администратора.
                if (firstAdminWindow.ShowDialog() != true)
                {
                    // Если пользователь просто закрыл окно, не создав админа,
                    // мы принудительно завершаем работу приложения.
                    Shutdown();
                    return;
                }
            }

            // Этот метод будет вызван только если администратор уже существует
            // или был только что успешно создан.
            ShowLoginWindow();
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow
            {
                DataContext = new LoginViewModel(_databaseService)
            };

            // Если вход в систему прошел успешно (ViewModel установил DialogResult в true)
            if (loginWindow.ShowDialog() == true)
            {
                var loginViewModel = (LoginViewModel)loginWindow.DataContext;

                var mainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(_databaseService, loginViewModel.LoggedInUser!)
                };

                // --- ИСПРАВЛЕНИЕ ЖИЗНЕННОГО ЦИКЛА 2 ---
                // Теперь, когда у нас есть настоящее главное окно, мы делаем его основным
                // и возвращаем стандартный режим завершения работы. Приложение будет работать,
                // пока пользователь не закроет это главное окно.
                Current.MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            else
            {
                // Если пользователь закрыл окно входа, не авторизовавшись,
                // мы принудительно завершаем работу приложения.
                Shutdown();
            }
        }
    }
}
