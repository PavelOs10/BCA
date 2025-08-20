using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Helpers;
using CheckpointApp.Models;

namespace CheckpointApp.ViewModels
{
    public partial class RegistrationViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private string _errorMessage;

        public RegistrationViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task RegisterUser(PasswordBox passwordBox)
        {
            var password = passwordBox.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Имя пользователя и пароль не могут быть пустыми.";
                return;
            }

            // Проверяем, не занято ли имя пользователя
            var existingUser = await _databaseService.GetUserByUsernameAsync(Username);
            if (existingUser != null)
            {
                ErrorMessage = "Это имя пользователя уже занято.";
                return;
            }

            var newUser = new User
            {
                Username = Username.ToUpper(),
                PasswordHash = PasswordHelper.HashPassword(password),
                IsAdmin = false // Новые пользователи по умолчанию не администраторы
            };

            bool success = await _databaseService.AddUserAsync(newUser);

            if (success)
            {
                MessageBox.Show("Новый оператор успешно зарегистрирован.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive)?.Close();
            }
            else
            {
                ErrorMessage = "Произошла ошибка при регистрации.";
            }
        }
    }
}
