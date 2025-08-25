using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using CheckpointApp.Services;
using CheckpointApp.Views;
using Microsoft.Win32;

namespace CheckpointApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly SecurityService _securityService;
        private readonly User _currentUser;
        private readonly ExcelExportService _excelExportService;
        private readonly WordExportService _wordExportService;
        private CancellationTokenSource? _filterCts;

        // --- НОВОЕ СВОЙСТВО: Флаг для смены оператора ---
        public bool IsSwitchingUserRequested { get; private set; } = false;

        public ObservableCollection<string> AllPurposes { get; set; }
        public ObservableCollection<string> AllDestinations { get; set; }
        public ObservableCollection<string> AllVehicleMakes { get; set; }
        public ObservableCollection<string> AllCitizenships { get; set; }
        public ObservableCollection<TempGood> TemporaryGoodsList { get; set; }

        [ObservableProperty] private ObservableCollection<Crossing> _allCrossings;
        public ICollectionView CrossingsView { get; }

        [ObservableProperty] private Person _currentPerson;
        [ObservableProperty] private DateTime? _currentPersonDob;
        [ObservableProperty] private Crossing _currentCrossing;
        [ObservableProperty] private Vehicle _currentVehicle;
        [ObservableProperty] private Crossing? _selectedCrossing;

        public List<string> CrossingTypes { get; } = new List<string> { "ПЕШКОМ", "ВОДИТЕЛЬ", "ПАССАЖИР" };
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsVehicleInfoEnabled))] private string _selectedCrossingType;

        public bool IsVehicleInfoEnabled => SelectedCrossingType == "ВОДИТЕЛЬ";
        public bool IsDirectionIn { get => CurrentCrossing.Direction == "ВЪЕЗД"; set { if (value) CurrentCrossing.Direction = "ВЪЕЗД"; OnPropertyChanged(); OnPropertyChanged(nameof(IsDirectionOut)); } }
        public bool IsDirectionOut { get => CurrentCrossing.Direction == "ВЫЕЗД"; set { if (value) CurrentCrossing.Direction = "ВЫЕЗД"; OnPropertyChanged(); OnPropertyChanged(nameof(IsDirectionIn)); } }

        [ObservableProperty] private string _windowTitle;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private bool _isAdmin;

        [ObservableProperty] private DateTime _dashboardStartDate;
        [ObservableProperty] private DateTime _dashboardEndDate;
        [ObservableProperty] private int _enteredPersonsCount;
        [ObservableProperty] private int _enteredVehiclesCount;
        [ObservableProperty] private int _exitedPersonsCount;
        [ObservableProperty] private int _exitedVehiclesCount;
        [ObservableProperty] private int _wantedPersonsTotalCount;
        [ObservableProperty] private int _watchlistPersonsTotalCount;
        [ObservableProperty] private int _personsInZoneCount;
        [ObservableProperty] private int _vehiclesInZoneCount;


        public MainViewModel(DatabaseService databaseService, User currentUser)
        {
            _databaseService = databaseService;
            _securityService = new SecurityService(databaseService);
            _currentUser = currentUser;
            _excelExportService = new ExcelExportService();
            _wordExportService = new WordExportService();

            _allCrossings = new ObservableCollection<Crossing>();
            CrossingsView = CollectionViewSource.GetDefaultView(_allCrossings);
            CrossingsView.Filter = FilterCrossings;

            AllPurposes = new ObservableCollection<string>();
            AllDestinations = new ObservableCollection<string>();
            AllVehicleMakes = new ObservableCollection<string>();
            AllCitizenships = new ObservableCollection<string>();
            TemporaryGoodsList = new ObservableCollection<TempGood>();

            IsAdmin = _currentUser.IsAdmin;
            WindowTitle = $"Контрольный пункт | Пользователь: {_currentUser.Username} ({(_currentUser.IsAdmin ? "Администратор" : "Оператор")})";

            _currentPerson = new Person();
            _currentCrossing = new Crossing();
            _currentVehicle = new Vehicle();
            _selectedCrossingType = CrossingTypes[0];
            _windowTitle = "";
            _statusMessage = "";

            _dashboardStartDate = DateTime.Today;
            _dashboardEndDate = DateTime.Today.AddDays(1).AddTicks(-1);

            InitializeNewEntry();
            _ = LoadInitialData();
        }

        private bool FilterCrossings(object obj)
        {
            if (obj is not Crossing crossing) return false;
            var lastNameFilter = CurrentPerson.LastName?.Trim();
            var firstNameFilter = CurrentPerson.FirstName?.Trim();
            var passportFilter = CurrentPerson.PassportData?.Trim();
            if (string.IsNullOrWhiteSpace(lastNameFilter) && string.IsNullOrWhiteSpace(firstNameFilter) && string.IsNullOrWhiteSpace(passportFilter)) return true;
            bool match = true;
            if (!string.IsNullOrWhiteSpace(lastNameFilter)) match &= crossing.FullName.StartsWith(lastNameFilter, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(firstNameFilter)) match &= crossing.FullName.Contains(firstNameFilter, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(passportFilter)) match &= crossing.PersonPassport.StartsWith(passportFilter, StringComparison.OrdinalIgnoreCase);
            return match;
        }

        private async void OnCurrentPersonPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Person.LastName) || e.PropertyName == nameof(Person.FirstName) || e.PropertyName == nameof(Person.PassportData))
            {
                _filterCts?.Cancel();
                _filterCts = new CancellationTokenSource();
                try
                {
                    await Task.Delay(300, _filterCts.Token);
                    Application.Current.Dispatcher.Invoke(() => CrossingsView.Refresh());
                }
                catch (TaskCanceledException) { /* Ignore */ }
            }
        }

        partial void OnSelectedCrossingTypeChanged(string value)
        {
            if (value != null)
            {
                CurrentCrossing.CrossingType = value;
            }
            if (!IsVehicleInfoEnabled)
            {
                CurrentVehicle = new Vehicle();
            }
        }

        private void InitializeNewEntry()
        {
            if (CurrentPerson != null)
            {
                CurrentPerson.PropertyChanged -= OnCurrentPersonPropertyChanged;
            }

            CurrentPerson = new Person();
            CurrentCrossing = new Crossing { Direction = "ВЪЕЗД" };
            CurrentVehicle = new Vehicle();
            CurrentPersonDob = null;
            SelectedCrossingType = CrossingTypes[0];
            CurrentCrossing.CrossingType = SelectedCrossingType;
            CurrentPerson.PropertyChanged += OnCurrentPersonPropertyChanged;
            TemporaryGoodsList.Clear();
            StatusMessage = "Готов к работе.";
            CrossingsView.Refresh();
        }

        private async Task LoadInitialData()
        {
            StatusMessage = "Загрузка данных...";

            var wantedTask = _databaseService.GetWantedPersonsAsync();
            var watchlistTask = _databaseService.GetWatchlistPersonsAsync();
            var crossingsTask = _databaseService.GetAllCrossingsAsync();
            var purposesTask = _databaseService.GetDistinctValuesAsync("crossings", "purpose");
            var destinationsTask = _databaseService.GetDistinctValuesAsync("crossings", "destination_town");
            var makesTask = _databaseService.GetDistinctValuesAsync("vehicles", "make");
            var citizenshipsTask = _databaseService.GetDistinctValuesAsync("persons", "citizenship");

            await Task.WhenAll(wantedTask, watchlistTask, crossingsTask, purposesTask,
                               destinationsTask, makesTask, citizenshipsTask);

            var wantedPersons = (await wantedTask).ToList();
            var watchlistPersons = (await watchlistTask).ToList();
            var crossings = (await crossingsTask).ToList();

            AllCrossings.Clear();
            foreach (var crossing in crossings)
            {
                if (!crossing.IsDeleted)
                {
                    var fullNameParts = crossing.FullName.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                    var lastName = fullNameParts.Length > 0 ? fullNameParts[0] : "";
                    var firstName = fullNameParts.Length > 1 ? fullNameParts[1] : "";

                    crossing.IsOnWantedList = wantedPersons.Any(wp =>
                        wp.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase) &&
                        wp.FirstName.Equals(firstName, StringComparison.OrdinalIgnoreCase) &&
                        wp.Dob == crossing.PersonDob);

                    if (!crossing.IsOnWantedList)
                    {
                        crossing.IsOnWatchlist = watchlistPersons.Any(wlp =>
                            wlp.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase) &&
                            wlp.FirstName.Equals(firstName, StringComparison.OrdinalIgnoreCase) &&
                            wlp.Dob == crossing.PersonDob);
                    }
                }
                AllCrossings.Add(crossing);
            }

            AllPurposes.Clear();
            foreach (var p in await purposesTask) AllPurposes.Add(p);
            AllDestinations.Clear();
            foreach (var d in await destinationsTask) AllDestinations.Add(d);
            AllVehicleMakes.Clear();
            foreach (var m in await makesTask) AllVehicleMakes.Add(m);
            AllCitizenships.Clear();
            foreach (var c in await citizenshipsTask) AllCitizenships.Add(c);

            await UpdateAllDashboardStats();

            StatusMessage = $"Загружено {AllCrossings.Count(c => !c.IsDeleted)} активных записей.";
        }

        [RelayCommand]
        private async Task SaveCrossing()
        {
            if (string.IsNullOrWhiteSpace(CurrentPerson.LastName) ||
                string.IsNullOrWhiteSpace(CurrentPerson.FirstName) ||
                string.IsNullOrWhiteSpace(CurrentPerson.PassportData) ||
                CurrentPersonDob == null)
            {
                MessageBox.Show("Заполните обязательные поля: Фамилия, Имя, Паспорт, Дата рождения.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (IsDirectionIn && string.IsNullOrWhiteSpace(CurrentCrossing.DestinationTown))
            {
                MessageBox.Show("Для направления 'Въезд' необходимо указать населенный пункт следования.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CurrentPerson.Dob = CurrentPersonDob.Value.ToString("dd.MM.yyyy");

            var securityResult = await _securityService.PerformChecksAsync(CurrentPerson);
            if (!securityResult.IsAllowed)
            {
                StatusMessage = "Операция сохранения отменена оператором.";
                return;
            }

            StatusMessage = "Сохранение...";
            try
            {
                CurrentCrossing.CrossingType = SelectedCrossingType;

                CurrentPerson.LastName = CurrentPerson.LastName.ToUpper();
                CurrentPerson.FirstName = CurrentPerson.FirstName.ToUpper();
                CurrentPerson.Patronymic = CurrentPerson.Patronymic?.ToUpper();
                CurrentPerson.PassportData = CurrentPerson.PassportData.ToUpper();
                CurrentPerson.Citizenship = CurrentPerson.Citizenship?.ToUpper() ?? "";
                CurrentCrossing.Purpose = CurrentCrossing.Purpose?.ToUpper();
                CurrentCrossing.DestinationTown = CurrentCrossing.DestinationTown?.ToUpper();
                CurrentVehicle.Make = CurrentVehicle.Make?.ToUpper() ?? "";
                CurrentVehicle.LicensePlate = CurrentVehicle.LicensePlate?.ToUpper() ?? "";

                var person = await _databaseService.FindPersonByPassportAsync(CurrentPerson.PassportData);
                int personId;

                if (person != null)
                {
                    bool nameMismatch = !person.LastName.Equals(CurrentPerson.LastName, StringComparison.OrdinalIgnoreCase) ||
                                        !person.FirstName.Equals(CurrentPerson.FirstName, StringComparison.OrdinalIgnoreCase);
                    bool dobMismatch = person.Dob != CurrentPerson.Dob;

                    if (nameMismatch || dobMismatch)
                    {
                        var message = "Внимание: данные в форме отличаются от записи в базе данных для этого паспорта.\n\n" +
                                      $"БД: {person.LastName} {person.FirstName}, {person.Dob}\n" +
                                      $"Форма: {CurrentPerson.LastName} {CurrentPerson.FirstName}, {CurrentPerson.Dob}\n\n" +
                                      "Продолжить сохранение для СУЩЕСТВУЮЩЕГО человека?";

                        if (MessageBox.Show(message, "Несоответствие данных", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        {
                            StatusMessage = "Сохранение отменено из-за несоответствия данных.";
                            return;
                        }
                    }
                    personId = person.Id;

                    if (person.Notes != CurrentPerson.Notes)
                    {
                        await _databaseService.UpdatePersonNotesAsync(personId, CurrentPerson.Notes);
                    }
                }
                else
                {
                    personId = await _databaseService.CreatePersonAsync(CurrentPerson);
                }

                if (personId > 0)
                {
                    var lastCrossing = await _databaseService.GetLastCrossingByPersonIdAsync(personId);
                    if (lastCrossing?.Direction == CurrentCrossing.Direction)
                    {
                        var message = $"Последнее пересечение для этого человека также было '{CurrentCrossing.Direction}'.\n\n" +
                                      "Вы уверены, что хотите сохранить дублирующее направление?";
                        if (MessageBox.Show(message, "Возможное дублирование", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                        {
                            StatusMessage = "Сохранение отменено оператором.";
                            return;
                        }
                    }
                }

                int? vehicleId = null;
                if (IsVehicleInfoEnabled && !string.IsNullOrWhiteSpace(CurrentVehicle.LicensePlate))
                {
                    var vehicle = await _databaseService.FindVehicleByLicensePlateAsync(CurrentVehicle.LicensePlate);
                    vehicleId = vehicle?.ID ?? await _databaseService.CreateVehicleAsync(CurrentVehicle);
                }

                CurrentCrossing.PersonId = personId;
                CurrentCrossing.VehicleId = vehicleId;
                CurrentCrossing.OperatorId = _currentUser.ID;
                CurrentCrossing.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                int newCrossingId = await _databaseService.CreateCrossingAsync(CurrentCrossing);

                if (TemporaryGoodsList.Any())
                {
                    var goodsToSave = TemporaryGoodsList.Select(g => new Good
                    {
                        CrossingId = newCrossingId,
                        Description = g.Description,
                        Quantity = g.Quantity,
                        Unit = g.Unit
                    });
                    await _databaseService.AddGoodsAsync(goodsToSave);
                }

                if (!string.IsNullOrWhiteSpace(CurrentCrossing.Purpose) && !AllPurposes.Contains(CurrentCrossing.Purpose)) AllPurposes.Add(CurrentCrossing.Purpose);
                if (!string.IsNullOrWhiteSpace(CurrentCrossing.DestinationTown) && !AllDestinations.Contains(CurrentCrossing.DestinationTown)) AllDestinations.Add(CurrentCrossing.DestinationTown);
                if (!string.IsNullOrWhiteSpace(CurrentVehicle.Make) && !AllVehicleMakes.Contains(CurrentVehicle.Make)) AllVehicleMakes.Add(CurrentVehicle.Make);
                if (!string.IsNullOrWhiteSpace(CurrentPerson.Citizenship) && !AllCitizenships.Contains(CurrentPerson.Citizenship)) AllCitizenships.Add(CurrentPerson.Citizenship);

                StatusMessage = $"Пересечение ID: {newCrossingId} успешно сохранено.";
                ClearForm();
                await LoadInitialData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при сохранении: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Ошибка сохранения.";
            }
        }

        [RelayCommand]
        private async Task SelectCrossing()
        {
            if (SelectedCrossing == null) return;

            if (SelectedCrossing.IsDeleted)
            {
                StatusMessage = "Нельзя выбрать ошибочную запись для создания нового пересечения.";
                return;
            }

            StatusMessage = "Загрузка данных...";
            try
            {
                var personFromDb = await _databaseService.FindPersonByPassportAsync(SelectedCrossing.PersonPassport);
                if (personFromDb == null)
                {
                    StatusMessage = "Не удалось найти данные выбранного человека.";
                    return;
                }

                CurrentPerson.PropertyChanged -= OnCurrentPersonPropertyChanged;
                CurrentPerson = personFromDb;
                CurrentPerson.PropertyChanged += OnCurrentPersonPropertyChanged;

                if (DateTime.TryParse(personFromDb.Dob, out var dob))
                {
                    CurrentPersonDob = dob;
                }

                if (SelectedCrossing.VehicleId.HasValue && !string.IsNullOrWhiteSpace(SelectedCrossing.VehicleInfo) && SelectedCrossing.VehicleInfo.Contains('/'))
                {
                    var plate = SelectedCrossing.VehicleInfo.Split('/')[1].Trim();
                    var vehicleFromDb = await _databaseService.FindVehicleByLicensePlateAsync(plate);
                    CurrentVehicle = vehicleFromDb ?? new Vehicle();
                }
                else
                {
                    CurrentVehicle = new Vehicle();
                }

                var newCrossing = new Crossing
                {
                    Purpose = SelectedCrossing.Purpose,
                    DestinationTown = SelectedCrossing.DestinationTown,
                    CrossingType = SelectedCrossing.CrossingType,
                    Direction = SelectedCrossing.Direction == "ВЪЕЗД" ? "ВЫЕЗД" : "ВЪЕЗД"
                };
                CurrentCrossing = newCrossing;

                SelectedCrossingType = CurrentCrossing.CrossingType;
                OnPropertyChanged(nameof(IsDirectionIn));
                OnPropertyChanged(nameof(IsDirectionOut));

                TemporaryGoodsList.Clear();

                StatusMessage = $"Данные для {CurrentPerson.LastName} загружены. Готово к созданию нового пересечения.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка при загрузке данных.";
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task UpdateAllDashboardStats()
        {
            StatusMessage = "Обновление статистики...";
            var periodStatsTask = _databaseService.GetDashboardStatsAsync(DashboardStartDate, DashboardEndDate);
            var inZoneStatsTask = _databaseService.GetInZoneStatsAsync();
            var wantedCountTask = _databaseService.GetWantedPersonsCountAsync();
            var watchlistCountTask = _databaseService.GetWatchlistPersonsCountAsync();

            await Task.WhenAll(periodStatsTask, inZoneStatsTask, wantedCountTask, watchlistCountTask);

            var periodStats = await periodStatsTask;
            EnteredPersonsCount = periodStats.EnteredPersons;
            EnteredVehiclesCount = periodStats.EnteredVehicles;
            ExitedPersonsCount = periodStats.ExitedPersons;
            ExitedVehiclesCount = periodStats.ExitedVehicles;

            var inZoneStats = await inZoneStatsTask;
            PersonsInZoneCount = inZoneStats.PersonCount;
            VehiclesInZoneCount = inZoneStats.VehicleCount;

            WantedPersonsTotalCount = await wantedCountTask;
            WatchlistPersonsTotalCount = await watchlistCountTask;

            StatusMessage = "Статистика обновлена.";
        }

        [RelayCommand]
        private void ShowGoodsWindow()
        {
            var goodsWindow = new GoodsWindow();
            goodsWindow.Owner = Application.Current.MainWindow;
            var viewModel = new GoodsViewModel(TemporaryGoodsList);
            goodsWindow.DataContext = viewModel;
            goodsWindow.ShowDialog();
        }

        [RelayCommand]
        private void ClearForm()
        {
            InitializeNewEntry();
        }

        [RelayCommand]
        private async Task RefreshData()
        {
            await LoadInitialData();
        }

        [RelayCommand]
        private async Task DeleteCrossing()
        {
            if (SelectedCrossing == null)
            {
                MessageBox.Show("Выберите пересечение для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedCrossing.IsDeleted)
            {
                MessageBox.Show("Эта запись уже помечена как ошибочная.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите пометить запись ID: {SelectedCrossing.ID} как ошибочную?\n\n" +
                                         "Это действие нельзя будет отменить.",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _databaseService.MarkCrossingAsDeletedAsync(SelectedCrossing.ID);
                StatusMessage = $"Запись ID: {SelectedCrossing.ID} помечена как ошибочная.";
                await LoadInitialData();
            }
        }

        [RelayCommand]
        private async Task GeneratePersonReport()
        {
            if (SelectedCrossing == null || SelectedCrossing.IsDeleted)
            {
                MessageBox.Show("Пожалуйста, выберите действующее пересечение в журнале, чтобы сформировать запрос.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Word Document (*.docx)|*.docx",
                FileName = $"Запрос_{SelectedCrossing.FullName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.docx",
                Title = "Сохранить запрос на лицо"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                StatusMessage = "Формирование отчета... Пожалуйста, подождите.";
                try
                {
                    var person = await _databaseService.FindPersonByPassportAsync(SelectedCrossing.PersonPassport);
                    if (person == null)
                    {
                        MessageBox.Show("Не удалось найти досье на выбранное лицо.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var vehicles = await _databaseService.GetVehiclesByPersonIdAsync(person.Id);
                    var crossings = await _databaseService.GetAllCrossingsByPersonIdAsync(person.Id);
                    var goodsSummary = await _databaseService.GetGoodsSummaryByPersonIdAsync(person.Id);

                    await _wordExportService.ExportPersonReportAsync(person, vehicles, crossings, goodsSummary, saveFileDialog.FileName);

                    StatusMessage = $"Отчет успешно сохранен: {saveFileDialog.FileName}";
                    MessageBox.Show("Отчет успешно сформирован и сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Ошибка при формировании отчета.";
                    MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- НОВАЯ КОМАНДА: Для смены оператора ---
        [RelayCommand]
        private void SwitchUser()
        {
            IsSwitchingUserRequested = true;
            // Находим и закрываем текущее главное окно
            Application.Current.MainWindow?.Close();
        }

        [RelayCommand]
        private void Exit()
        {
            Application.Current.MainWindow?.Close();
        }
        [RelayCommand]
        private async Task ManageWantedList()
        {
            var window = new WantedListManagementWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new WantedListManagementViewModel(_databaseService)
            };
            window.ShowDialog();
            await UpdateAllDashboardStats();
        }
        [RelayCommand]
        private async Task ManageWatchlist()
        {
            var window = new WatchlistManagementWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new WatchlistManagementViewModel(_databaseService)
            };
            window.ShowDialog();
            await UpdateAllDashboardStats();
        }
        [RelayCommand]
        private void ManageUsers()
        {
            var window = new UserManagementWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new UserManagementViewModel(_databaseService, _currentUser)
            };
            window.ShowDialog();
        }
        [RelayCommand]
        private void ShowPeopleInZone()
        {
            var window = new PeopleInZoneWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new PeopleInZoneViewModel(_databaseService)
            };
            window.ShowDialog();
        }
        [RelayCommand]
        private void ShowAnalytics()
        {
            var window = new AnalyticsWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new AnalyticsViewModel(_databaseService)
            };
            window.Show();
        }
        [RelayCommand]
        private async Task ExportToExcel()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"Журнал_пересечений_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Сохранить журнал в Excel"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                StatusMessage = "Экспорт данных в Excel... Пожалуйста, подождите.";
                try
                {
                    var dataToExport = CrossingsView.Cast<Crossing>().Where(c => !c.IsDeleted).ToList();
                    await _excelExportService.ExportCrossingsAsync(dataToExport, saveFileDialog.FileName);
                    StatusMessage = $"Экспорт успешно завершен. Файл сохранен: {saveFileDialog.FileName}";
                    MessageBox.Show("Данные успешно экспортированы.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Ошибка при экспорте.";
                    MessageBox.Show($"Произошла ошибка во время экспорта: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ShowAboutInfo()
        {
            MessageBox.Show("Версия 4.0, Разработчик ОПО Ленингор", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
