using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Helpers;
using CheckpointApp.Models;
using CheckpointApp.Views;

namespace CheckpointApp.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private string _errorMessage;

        public User LoggedInUser { get; private set; }

        public LoginViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task Login(PasswordBox passwordBox)
        {
            var password = passwordBox.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Введите имя пользователя и пароль.";
                return;
            }

            var user = await _databaseService.GetUserByUsernameAsync(Username);

            if (user != null && PasswordHelper.VerifyPassword(password, user.PasswordHash))
            {
                LoggedInUser = user;
                // Успешный вход
                Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive).DialogResult = true;
            }
            else
            {
                ErrorMessage = "Неверное имя пользователя или пароль.";
            }
        }

        [RelayCommand]
        private void Register()
        {
            // Открываем окно регистрации нового оператора (без прав администратора)
            var registrationWindow = new RegistrationWindow();
            var registrationViewModel = new RegistrationViewModel(_databaseService);
            registrationWindow.DataContext = registrationViewModel;
            registrationWindow.ShowDialog();
        }
    }
}
