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
                    break;
                }

                if (loginViewModel.LoggedInUser == null)
                {
                    MessageBox.Show("Произошла ошибка аутентификации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                }

                var mainViewModel = new MainViewModel(databaseService, loginViewModel.LoggedInUser);

                bool isDataLoadedSuccessfully = await mainViewModel.LoadDataAsync();

                if (!isDataLoadedSuccessfully)
                {
                    break;
                }

                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                Current.MainWindow = mainWindow;
                mainWindow.ShowDialog();

                // --- ИЗМЕНЕНИЕ: Добавлена небольшая задержка ---
                await Task.Delay(100);

                if (!mainViewModel.IsSwitchingUserRequested)
                {
                    break;
                }
            }

            Shutdown();
        }
    }
}

