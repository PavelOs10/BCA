using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Helpers;
using CheckpointApp.Models;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CheckpointApp.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly User _currentUser; // Текущий залогиненный пользователь

        [ObservableProperty]
        private ObservableCollection<User> _users;

        [ObservableProperty]
        private User _selectedUser;

        // Свойства для формы добавления
        [ObservableProperty]
        private string _newUsername;
        [ObservableProperty]
        private bool _isNewUserAdmin;

        public UserManagementViewModel(DatabaseService databaseService, User currentUser)
        {
            _databaseService = databaseService;
            _currentUser = currentUser;
            _ = LoadUsersAsync();
        }

        private async Task LoadUsersAsync()
        {
            var userList = await _databaseService.GetAllUsersAsync();
            Users = new ObservableCollection<User>(userList);
        }

        [RelayCommand]
        private async Task AddUser(PasswordBox passwordBox)
        {
            var password = passwordBox.Password;
            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Имя пользователя и пароль не могут быть пустыми.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingUser = await _databaseService.GetUserByUsernameAsync(NewUsername);
            if (existingUser != null)
            {
                MessageBox.Show("Пользователь с таким именем уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newUser = new User
            {
                Username = NewUsername.ToUpper(),
                PasswordHash = PasswordHelper.HashPassword(password),
                IsAdmin = IsNewUserAdmin
            };

            await _databaseService.AddUserAsync(newUser);
            MessageBox.Show("Новый пользователь успешно добавлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

            // Очистка и обновление
            NewUsername = string.Empty;
            passwordBox.Clear();
            IsNewUserAdmin = false;
            await LoadUsersAsync();
        }

        [RelayCommand]
        private async Task DeleteUser()
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Выберите пользователя для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // --- Ключевая проверка: запрет на удаление самого себя ---
            if (SelectedUser.ID == _currentUser.ID)
            {
                MessageBox.Show("Вы не можете удалить свою собственную учетную запись.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить пользователя '{SelectedUser.Username}'?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _databaseService.DeleteUserAsync(SelectedUser.ID);
                await LoadUsersAsync();
            }
        }
    }
}
