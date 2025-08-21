using System.Linq;
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
    public partial class FirstAdminViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public FirstAdminViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task CreateAdmin(PasswordBox passwordBox)
        {
            var password = passwordBox.Password;
            var confirmPasswordBox = (passwordBox.Parent as Grid)?.FindName("ConfirmPasswordBox") as PasswordBox;
            var confirmPassword = confirmPasswordBox?.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Имя пользователя и пароль не могут быть пустыми.";
                return;
            }

            if (password != confirmPassword)
            {
                ErrorMessage = "Пароли не совпадают.";
                return;
            }

            var newUser = new User
            {
                Username = Username.ToUpper(),
                PasswordHash = PasswordHelper.HashPassword(password),
                IsAdmin = true
            };

            bool success = await _databaseService.AddUserAsync(newUser);

            if (success)
            {
                MessageBox.Show("Учетная запись администратора успешно создана.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                var activeWindow = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                if (activeWindow != null)
                {
                    activeWindow.DialogResult = true;
                }
            }
            else
            {
                ErrorMessage = "Не удалось создать пользователя. Возможно, имя пользователя уже занято.";
            }
        }
    }
}