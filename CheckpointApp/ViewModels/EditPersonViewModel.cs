using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CheckpointApp.ViewModels
{
    public partial class EditPersonViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private Person _personToEdit;

        [ObservableProperty]
        private DateTime? _personDob;

        public EditPersonViewModel(DatabaseService databaseService, Person person)
        {
            _databaseService = databaseService;
            _personToEdit = person;

            // --- ИСПРАВЛЕНИЕ 2: Использование строгого формата для разбора даты ---
            if (DateTime.TryParseExact(person.Dob, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            {
                PersonDob = dob;
            }
        }

        [RelayCommand]
        private async Task SaveChanges()
        {
            // --- ИСПРАВЛЕНИЕ 4: Добавлена проверка паспортных данных ---
            if (string.IsNullOrWhiteSpace(PersonToEdit.LastName) ||
                string.IsNullOrWhiteSpace(PersonToEdit.FirstName) ||
                string.IsNullOrWhiteSpace(PersonToEdit.PassportData) ||
                PersonDob == null)
            {
                MessageBox.Show("Фамилия, Имя, Паспортные данные и Дата рождения не могут быть пустыми.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- ИСПРАВЛЕНИЕ 4: Проверка, что новый номер паспорта не занят другим человеком ---
            var existingPerson = await _databaseService.FindPersonByPassportAsync(PersonToEdit.PassportData.ToUpper());
            if (existingPerson != null && existingPerson.Id != PersonToEdit.Id)
            {
                MessageBox.Show("Человек с такими паспортными данными уже существует в базе.", "Ошибка: Дубликат", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            PersonToEdit.Dob = PersonDob.Value.ToString("dd.MM.yyyy");

            // Приведение к верхнему регистру для единообразия
            PersonToEdit.LastName = PersonToEdit.LastName.ToUpper();
            PersonToEdit.FirstName = PersonToEdit.FirstName.ToUpper();
            PersonToEdit.Patronymic = PersonToEdit.Patronymic?.ToUpper();
            PersonToEdit.Citizenship = PersonToEdit.Citizenship?.ToUpper() ?? "";
            PersonToEdit.PassportData = PersonToEdit.PassportData.ToUpper();

            bool success = await _databaseService.UpdatePersonAsync(PersonToEdit);

            if (success)
            {
                MessageBox.Show("Данные успешно обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                // Закрываем окно с результатом OK
                var activeWindow = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                if (activeWindow != null)
                {
                    activeWindow.DialogResult = true;
                }
            }
            else
            {
                MessageBox.Show("Не удалось обновить данные.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
