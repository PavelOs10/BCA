using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace CheckpointApp.ViewModels
{
    public partial class PeopleInZoneViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        [ObservableProperty] private ObservableCollection<PersonInZone> _personsInZone;
        [ObservableProperty] private ObservableCollection<VehicleInZone> _vehiclesInZone;
        [ObservableProperty] private string _statusText;

        public PeopleInZoneViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _personsInZone = new ObservableCollection<PersonInZone>();
            _vehiclesInZone = new ObservableCollection<VehicleInZone>();
            _statusText = "Загрузка данных...";
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadData()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            StatusText = "Загрузка данных...";
            var personsTask = _databaseService.GetPersonsInZoneAsync();
            var vehiclesTask = _databaseService.GetVehiclesInZoneAsync();

            await Task.WhenAll(personsTask, vehiclesTask);

            var personList = (await personsTask).ToList();
            var vehicleList = (await vehiclesTask).ToList();

            Application.Current.Dispatcher.Invoke(() => {
                PersonsInZone.Clear();
                foreach (var p in personList) PersonsInZone.Add(p);

                VehiclesInZone.Clear();
                foreach (var v in vehicleList) VehiclesInZone.Add(v);

                StatusText = $"В погранзоне: {personList.Count} чел. и {vehicleList.Count} ТС | Обновлено: {DateTime.Now:G}";
            });
        }
    }
}
