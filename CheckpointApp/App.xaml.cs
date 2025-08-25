using System.Linq;
using System.Windows;
using CheckpointApp.DataAccess;
using CheckpointApp.ViewModels;
using CheckpointApp.Views;

namespace CheckpointApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly DatabaseService _databaseService;

        public App()
        {
            _databaseService = new DatabaseService();
        }

        /// <summary>
        /// Логика, выполняемая при запуске приложения.
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Устанавливаем режим завершения работы приложения вручную,
            // чтобы оно не закрылось после первого окна.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Проверяем, есть ли в базе данных хотя бы один пользователь.
            var userCount = await _databaseService.GetUserCountAsync();

            // Если пользователей нет (первый запуск), открываем окно
            // для создания учетной записи администратора.
            if (userCount == 0)
            {
                var firstAdminWindow = new FirstAdminWindow
                {
                    DataContext = new FirstAdminViewModel(_databaseService)
                };

                // Если администратор не был создан, завершаем работу приложения.
                if (firstAdminWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            // --- ИЗМЕНЕНИЕ: Запускаем бесконечный цикл аутентификации ---
            // Этот цикл позволит нам возвращаться к окну входа после смены пользователя.
            while (true)
            {
                var loginViewModel = new LoginViewModel(_databaseService);
                var loginWindow = new LoginWindow
                {
                    DataContext = loginViewModel
                };

                // Показываем окно входа как диалоговое.
                // Если пользователь закрыл его, не авторизовавшись, выходим из цикла и завершаем приложение.
                if (loginWindow.ShowDialog() != true)
                {
                    break; // Выход из цикла
                }

                // Если вход успешен, создаем и показываем главное окно.
                var mainViewModel = new MainViewModel(_databaseService, loginViewModel.LoggedInUser!);
                var mainWindow = new Views.MainWindow
                {
                    DataContext = mainViewModel
                };

                // Назначаем главное окно.
                Current.MainWindow = mainWindow;
                mainWindow.ShowDialog(); // Показываем как диалог, чтобы код ждал его закрытия

                // После закрытия MainWindow проверяем, была ли запрошена смена пользователя.
                // Если нет, то пользователь просто закрыл программу. Выходим из цикла.
                if (!mainViewModel.IsSwitchingUserRequested)
                {
                    break; // Выход из цикла
                }
                // Если IsSwitchingUserRequested = true, цикл начнется заново, и появится окно входа.
            }

            // Завершаем работу приложения, когда цикл прерывается.
            Shutdown();
        }
    }
}
