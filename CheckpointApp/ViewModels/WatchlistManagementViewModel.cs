using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CheckpointApp.ViewModels
{
    public partial class WatchlistManagementViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<WatchlistPerson> _watchlistPersons;

        [ObservableProperty]
        private WatchlistPerson _newWatchlistPerson;

        [ObservableProperty]
        private DateTime? _newWatchlistPersonDob;

        [ObservableProperty]
        private WatchlistPerson _selectedPerson;

        [ObservableProperty]
        private string _statusText;

        public WatchlistManagementViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            NewWatchlistPerson = new WatchlistPerson();
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var persons = await _databaseService.GetWatchlistPersonsAsync();
            WatchlistPersons = new ObservableCollection<WatchlistPerson>(persons);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusText = $"Всего в списке: {WatchlistPersons.Count} чел. | Последнее обновление: {DateTime.Now:G}";
        }

        [RelayCommand]
        private async Task AddPerson()
        {
            if (string.IsNullOrWhiteSpace(NewWatchlistPerson.LastName) ||
                string.IsNullOrWhiteSpace(NewWatchlistPerson.FirstName) ||
                NewWatchlistPersonDob == null)
            {
                MessageBox.Show("Поля 'Фамилия', 'Имя' и 'Дата рождения' обязательны.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewWatchlistPerson.Dob = NewWatchlistPersonDob.Value.ToString("dd.MM.yyyy");
            NewWatchlistPerson.LastName = NewWatchlistPerson.LastName.ToUpper();
            NewWatchlistPerson.FirstName = NewWatchlistPerson.FirstName.ToUpper();
            NewWatchlistPerson.Patronymic = NewWatchlistPerson.Patronymic?.ToUpper();

            await _databaseService.AddWatchlistPersonAsync(NewWatchlistPerson);
            MessageBox.Show("Запись успешно добавлена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

            ClearForm();
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task DeletePerson()
        {
            if (SelectedPerson == null)
            {
                MessageBox.Show("Выберите запись для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить запись для '{SelectedPerson.LastName} {SelectedPerson.FirstName}' из списка наблюдения?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _databaseService.DeleteWatchlistPersonAsync(SelectedPerson.ID);
                await LoadDataAsync();
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            NewWatchlistPerson = new WatchlistPerson();
            NewWatchlistPersonDob = null;
        }
    }
}
