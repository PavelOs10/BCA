using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;

namespace CheckpointApp.ViewModels
{
    public partial class WatchlistManagementViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ObservableCollection<WatchlistPerson> _allWatchlistPersons;

        public ICollectionView WatchlistPersonsView { get; }

        [ObservableProperty]
        private WatchlistPerson _newWatchlistPerson;

        [ObservableProperty]
        private DateTime? _newWatchlistPersonDob;

        [ObservableProperty]
        private WatchlistPerson? _selectedPerson;

        [ObservableProperty]
        private string _statusText;

        [ObservableProperty]
        private string _filterText = string.Empty;

        public WatchlistManagementViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _allWatchlistPersons = new ObservableCollection<WatchlistPerson>();
            WatchlistPersonsView = CollectionViewSource.GetDefaultView(_allWatchlistPersons);
            WatchlistPersonsView.Filter = FilterPersons;

            _newWatchlistPerson = new WatchlistPerson();
            _statusText = "";
            _ = LoadDataAsync();
        }

        private bool FilterPersons(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterText))
                return true;

            if (obj is WatchlistPerson person)
            {
                return person.LastName.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase) ||
                       person.FirstName.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        partial void OnFilterTextChanged(string value)
        {
            WatchlistPersonsView.Refresh();
        }

        private async Task LoadDataAsync()
        {
            var persons = await _databaseService.GetWatchlistPersonsAsync();
            _allWatchlistPersons.Clear();
            foreach (var person in persons)
            {
                _allWatchlistPersons.Add(person);
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusText = $"Всего в списке: {_allWatchlistPersons.Count} чел. | Последнее обновление: {DateTime.Now:G}";
        }

        [RelayCommand]
        private async Task AddPerson()
        {
            if (string.IsNullOrWhiteSpace(NewWatchlistPerson.LastName) ||
                string.IsNullOrWhiteSpace(NewWatchlistPerson.FirstName))
            {
                MessageBox.Show("Поля 'Фамилия' и 'Имя' обязательны для заполнения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewWatchlistPerson.Dob = NewWatchlistPersonDob.HasValue ? NewWatchlistPersonDob.Value.ToString("dd.MM.yyyy") : string.Empty;
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
