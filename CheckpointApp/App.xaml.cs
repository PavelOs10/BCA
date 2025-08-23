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
            // Инициализация сервиса для работы с базой данных
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

            // После проверки или создания администратора, показываем окно входа.
            ShowLoginWindow();
        }

        /// <summary>
        /// Отображает окно входа и обрабатывает результат.
        /// </summary>
        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow
            {
                DataContext = new LoginViewModel(_databaseService)
            };

            // Показываем окно входа как диалоговое.
            // Если вход успешен (окно возвращает true)...
            if (loginWindow.ShowDialog() == true)
            {
                var loginViewModel = (LoginViewModel)loginWindow.DataContext;

                // ...создаем и показываем главное окно приложения.
                var mainWindow = new Views.MainWindow
                {
                    DataContext = new MainViewModel(_databaseService, loginViewModel.LoggedInUser!)
                };

                // Назначаем главное окно и устанавливаем режим завершения работы
                // при его закрытии.
                Current.MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            // Если пользователь закрыл окно входа, не авторизовавшись,
            // завершаем работу приложения.
            else
            {
                Shutdown();
            }
        }
    }
}
