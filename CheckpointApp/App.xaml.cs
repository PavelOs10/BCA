using System.Windows;
using CheckpointApp.DataAccess;
using CheckpointApp.Views;
using CheckpointApp.ViewModels;

namespace CheckpointApp
{
    public partial class App : Application
    {
        private DatabaseService _databaseService;

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
                // Если пользователей нет, открываем окно создания первого администратора
                var firstAdminWindow = new FirstAdminWindow();
                var firstAdminViewModel = new FirstAdminViewModel(_databaseService);
                firstAdminWindow.DataContext = firstAdminViewModel;

                if (firstAdminWindow.ShowDialog() == true)
                {
                    // После успешного создания админа, показываем окно входа
                    ShowLoginWindow();
                }
                else
                {
                    // Если пользователь закрыл окно, завершаем приложение
                    Shutdown();
                }
            }
            else
            {
                ShowLoginWindow();
            }
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow();
            var loginViewModel = new LoginViewModel(_databaseService);
            loginWindow.DataContext = loginViewModel;

            if (loginWindow.ShowDialog() == true)
            {
                // Если логин успешен, открываем главное окно
                var mainWindow = new MainWindow();
                // Передаем залогиненного пользователя в MainViewModel
                var mainViewModel = new MainViewModel(_databaseService, loginViewModel.LoggedInUser);
                mainWindow.DataContext = mainViewModel;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
