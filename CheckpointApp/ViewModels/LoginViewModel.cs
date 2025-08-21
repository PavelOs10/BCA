using System.Diagnostics; // <-- Добавляем using для логирования
using System.Linq;
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
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public User? LoggedInUser { get; private set; }

        public LoginViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task Login(PasswordBox passwordBox)
        {
            var password = passwordBox.Password;
            Debug.WriteLine("--- Попытка входа ---");

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Введите имя пользователя и пароль.";
                Debug.WriteLine("ОШИБКА: Имя пользователя или пароль пустые.");
                return;
            }

            Debug.WriteLine($"Поиск пользователя: '{Username.ToUpper()}'");
            var user = await _databaseService.GetUserByUsernameAsync(Username);

            if (user != null)
            {
                Debug.WriteLine($"Пользователь '{user.Username}' найден в базе данных.");
                Debug.WriteLine($"Хэш в базе данных: {user.PasswordHash}");

                string hashOfInput = PasswordHelper.HashPassword(password);
                Debug.WriteLine($"Хэш введенного пароля: {hashOfInput}");

                bool isPasswordCorrect = PasswordHelper.VerifyPassword(password, user.PasswordHash);
                Debug.WriteLine($"Результат проверки пароля: {isPasswordCorrect}");

                if (isPasswordCorrect)
                {
                    Debug.WriteLine("УСПЕХ: Пароль верный. Вход разрешен.");
                    LoggedInUser = user;
                    var activeWindow = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                    if (activeWindow != null)
                    {
                        activeWindow.DialogResult = true;
                    }
                }
                else
                {
                    Debug.WriteLine("ОШИБКА: Пароль неверный.");
                    ErrorMessage = "Неверное имя пользователя или пароль.";
                }
            }
            else
            {
                Debug.WriteLine($"ОШИБКА: Пользователь '{Username.ToUpper()}' не найден в базе данных.");
                ErrorMessage = "Неверное имя пользователя или пароль.";
            }
            Debug.WriteLine("--- Конец попытки входа ---");
        }

        [RelayCommand]
        private void Register()
        {
            var registrationWindow = new RegistrationWindow
            {
                DataContext = new RegistrationViewModel(_databaseService)
            };
            registrationWindow.ShowDialog();
        }
    }
}
