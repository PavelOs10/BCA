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
    public partial class WantedListManagementViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<WantedPerson> _wantedPersons;

        [ObservableProperty]
        private WantedPerson _newWantedPerson;

        [ObservableProperty]
        private DateTime? _newWantedPersonDob;

        [ObservableProperty]
        private WantedPerson _selectedPerson;

        [ObservableProperty]
        private string _statusText;

        public WantedListManagementViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            NewWantedPerson = new WantedPerson();
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var persons = await _databaseService.GetWantedPersonsAsync();
            WantedPersons = new ObservableCollection<WantedPerson>(persons);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusText = $"Всего в списке: {WantedPersons.Count} чел. | Последнее обновление: {DateTime.Now:G}";
        }

        [RelayCommand]
        private async Task AddPerson()
        {
            if (string.IsNullOrWhiteSpace(NewWantedPerson.LastName) ||
                string.IsNullOrWhiteSpace(NewWantedPerson.FirstName) ||
                NewWantedPersonDob == null)
            {
                MessageBox.Show("Поля 'Фамилия', 'Имя' и 'Дата рождения' обязательны для заполнения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewWantedPerson.Dob = NewWantedPersonDob.Value.ToString("dd.MM.yyyy");
            NewWantedPerson.LastName = NewWantedPerson.LastName.ToUpper();
            NewWantedPerson.FirstName = NewWantedPerson.FirstName.ToUpper();
            NewWantedPerson.Patronymic = NewWantedPerson.Patronymic?.ToUpper();

            await _databaseService.AddWantedPersonAsync(NewWantedPerson);
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

            var result = MessageBox.Show($"Вы уверены, что хотите удалить запись для '{SelectedPerson.LastName} {SelectedPerson.FirstName}'?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _databaseService.DeleteWantedPersonAsync(SelectedPerson.ID);
                await LoadDataAsync();
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            NewWantedPerson = new WantedPerson();
            NewWantedPersonDob = null;
        }
    }
}
