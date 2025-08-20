using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;

namespace CheckpointApp.ViewModels
{
    using GroupedPersonsCollection = Dictionary<string, List<PersonInZone>>;
    public partial class PeopleInZoneViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        [ObservableProperty] private GroupedPersonsCollection _groupedPersons;
        [ObservableProperty] private string _statusText;
        public PeopleInZoneViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _groupedPersons = new GroupedPersonsCollection();
            _statusText = "";
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadData()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var persons = await _databaseService.GetPersonsInZoneAsync();
            var personList = persons.ToList();

            // Группируем данные по DestinationTown
            var grouped = personList
                .GroupBy(p => p.DestinationTown ?? "НЕ УКАЗАНО")
                .ToDictionary(g => g.Key, g => g.ToList());

            GroupedPersons = new GroupedPersonsCollection(grouped);

            StatusText = $"Всего в погранзоне: {personList.Count} чел. | Обновлено: {DateTime.Now:G}";
        }
    }
}
