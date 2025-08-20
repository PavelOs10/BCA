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

            var userCount = await _databaseService.GetUserCountAsync();

            if (userCount == 0)
            {
                var firstAdminWindow = new FirstAdminWindow
                {
                    DataContext = new FirstAdminViewModel(_databaseService)
                };

                if (firstAdminWindow.ShowDialog() != true)
                {
                    // Если пользователь не создал администратора, закрываем приложение
                    Shutdown();
                    return;
                }
            }

            // В любом случае после проверки показываем окно входа
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
                // Если логин успешен, получаем ViewModel, который теперь содержит данные о пользователе
                var loginViewModel = (LoginViewModel)loginWindow.DataContext;

                // Создаем главное окно
                var mainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(_databaseService, loginViewModel.LoggedInUser!)
                };

                // --- ВАЖНОЕ ИСПРАВЛЕНИЕ ---
                // Назначаем созданное окно главным окном приложения.
                // Теперь приложение будет работать, пока это окно не закроется.
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                // Если пользователь закрыл окно входа, завершаем приложение
                Shutdown();
            }
        }
    }
}
