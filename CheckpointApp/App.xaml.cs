using System.Windows;
using System.Threading.Tasks;
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
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var databaseService = new DatabaseService();
            databaseService.InitializeDatabase();

            if (await databaseService.GetUserCountAsync() == 0)
            {
                var firstAdminWindow = new FirstAdminWindow
                {
                    DataContext = new FirstAdminViewModel(databaseService)
                };
                if (firstAdminWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            while (true)
            {
                var loginViewModel = new LoginViewModel(databaseService);
                var loginWindow = new LoginWindow
                {
                    DataContext = loginViewModel
                };

                if (loginWindow.ShowDialog() != true)
                {
                    break; // Пользователь отменил вход
                }

                if (loginViewModel.LoggedInUser == null)
                {
                    MessageBox.Show("Произошла ошибка аутентификации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                }

                // --- ИЗМЕНЕНИЕ ЛОГИКИ ЗАПУСКА ---
                // 1. Создаем ViewModel
                var mainViewModel = new MainViewModel(databaseService, loginViewModel.LoggedInUser);

                // 2. Асинхронно и с ожиданием загружаем все необходимые данные
                bool isDataLoadedSuccessfully = await mainViewModel.LoadDataAsync();

                // 3. Если при загрузке произошла ошибка, не продолжаем и выходим
                if (!isDataLoadedSuccessfully)
                {
                    // Сообщение об ошибке будет показано внутри метода LoadDataAsync
                    break;
                }

                // 4. Только после успешной загрузки данных создаем и показываем главное окно
                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                Current.MainWindow = mainWindow;
                mainWindow.ShowDialog(); // Ожидаем закрытия главного окна

                // Если пользователь закрыл окно, а не нажал "Сменить оператора"
                if (!mainViewModel.IsSwitchingUserRequested)
                {
                    break;
                }
            }

            Shutdown();
        }
    }
}
