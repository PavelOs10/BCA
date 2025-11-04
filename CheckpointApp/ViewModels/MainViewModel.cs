using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using CheckpointApp.Services;
using CheckpointApp.Views;
using Microsoft.Win32;
using System.IO;

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

        public bool IsSwitchingUserRequested { get; private set; } = false;

        #region Collections for ComboBoxes and Lists
        public ObservableCollection<string> AllPurposes { get; set; }
        public ObservableCollection<string> AllDestinations { get; set; }
        public ObservableCollection<string> AllVehicleMakes { get; set; }
        public ObservableCollection<string> AllCitizenships { get; set; }
        public ObservableCollection<TempGood> TemporaryGoodsList { get; set; }
        [ObservableProperty] private ObservableCollection<Crossing> _allCrossings;
        public ICollectionView CrossingsView { get; }
        #endregion

        #region Current Entry Properties
        [ObservableProperty] private Person _currentPerson;
        [ObservableProperty] private DateTime? _currentPersonDob;
        [ObservableProperty] private Crossing _currentCrossing;
        [ObservableProperty] private Vehicle _currentVehicle;
        [ObservableProperty] private Crossing? _selectedCrossing;
        public List<string> CrossingTypes { get; } = new List<string> { "ПЕШКОМ", "ВОДИТЕЛЬ", "ПАССАЖИР" };
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsVehicleInfoEnabled))] private string _selectedCrossingType;
        public bool IsVehicleInfoEnabled => SelectedCrossingType == "ВОДИТЕЛЬ" || SelectedCrossingType == "ПАССАЖИР";
        public bool IsDirectionIn { get => CurrentCrossing.Direction == "ВЪЕЗД"; set { if (value) CurrentCrossing.Direction = "ВЪЕЗД"; OnPropertyChanged(); OnPropertyChanged(nameof(IsDirectionOut)); } }
        public bool IsDirectionOut { get => CurrentCrossing.Direction == "ВЫЕЗД"; set { if (value) CurrentCrossing.Direction = "ВЫЕЗД"; OnPropertyChanged(); OnPropertyChanged(nameof(IsDirectionIn)); } }
        #endregion

        #region UI and State Properties
        [ObservableProperty] private string _windowTitle;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private bool _isAdmin;

        [ObservableProperty] private string _securityCheckStatus = "Данные не проверялись";
        [ObservableProperty] private Brush _securityCheckColor = Brushes.Transparent;
        #endregion

        #region Dashboard Properties
        [ObservableProperty] private DateTime _dashboardStartDate;
        [ObservableProperty] private DateTime _dashboardEndDate;
        [ObservableProperty] private DateTime _dashboardStartTime;
        [ObservableProperty] private DateTime _dashboardEndTime;
        [ObservableProperty] private int _enteredPersonsCount;
        [ObservableProperty] private int _enteredVehiclesCount;
        [ObservableProperty] private int _exitedPersonsCount;
        [ObservableProperty] private int _exitedVehiclesCount;
        [ObservableProperty] private int _wantedPersonsTotalCount;
        [ObservableProperty] private int _watchlistPersonsTotalCount;
        [ObservableProperty] private int _personsInZoneCount;
        [ObservableProperty] private int _vehiclesInZoneCount;
        #endregion

        #region Filtering Properties
        [ObservableProperty] private string? _filterCitizenship;
        [ObservableProperty] private string? _filterPurpose;
        [ObservableProperty] private string? _filterVehicle;
        private int? _filterPersonId = null;
        private bool _isDateFilterActive = false;
        #endregion

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPassengerEntryMode))]
        [NotifyPropertyChangedFor(nameof(IsTravelInfoEditable))]
        private Crossing? _activeDriverCrossing;
        public bool IsPassengerEntryMode => ActiveDriverCrossing != null;

        public bool IsTravelInfoEditable => !IsPassengerEntryMode;

        public bool IsDriverSelected => SelectedCrossing != null && SelectedCrossing.CrossingType == "ВОДИТЕЛЬ" && !SelectedCrossing.IsDeleted;

        private HashSet<int> _previousPassengerCrossingIds = new HashSet<int>();
        private bool _isShowingPreviousPassengers = false;


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
            _dashboardEndDate = DateTime.Today;
            _dashboardStartTime = DateTime.Today;
            _dashboardEndTime = DateTime.Today.AddDays(1).AddTicks(-1);

            InitializeNewEntry();
        }

        private DateTime GetCombinedDateTime(DateTime date, DateTime time)
        {
            return date.Date + time.TimeOfDay;
        }

        private bool FilterCrossings(object obj)
        {
            if (obj is not Crossing crossing) return false;

            if (_isShowingPreviousPassengers)
            {
                if (!string.IsNullOrWhiteSpace(CurrentPerson.LastName) ||
                    !string.IsNullOrWhiteSpace(CurrentPerson.FirstName) ||
                    !string.IsNullOrWhiteSpace(CurrentPerson.Patronymic) ||
                    !string.IsNullOrWhiteSpace(CurrentPerson.Citizenship) ||
                    !string.IsNullOrWhiteSpace(CurrentPerson.PassportData))
                {
                    _isShowingPreviousPassengers = false;
                }
                else
                {
                    return _previousPassengerCrossingIds.Contains(crossing.ID);
                }
            }


            if (_filterPersonId.HasValue)
            {
                return crossing.PersonId == _filterPersonId.Value;
            }

            if (_isDateFilterActive)
            {
                if (DateTime.TryParse(crossing.Timestamp, out var timestamp))
                {
                    var finalStartDate = GetCombinedDateTime(DashboardStartDate, DashboardStartTime);
                    var finalEndDate = GetCombinedDateTime(DashboardEndDate, DashboardEndTime);
                    return timestamp >= finalStartDate && timestamp <= finalEndDate;
                }
                return false;
            }

            var lastNameFilter = CurrentPerson.LastName?.Trim();
            var firstNameFilter = CurrentPerson.FirstName?.Trim();
            var patronymicFilter = CurrentPerson.Patronymic?.Trim();
            var citizenshipFilter = CurrentPerson.Citizenship?.Trim();
            var passportFilter = CurrentPerson.PassportData?.Trim();

            var fullNameParts = crossing.FullName.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            var crossingLastName = fullNameParts.Length > 0 ? fullNameParts[0] : "";
            var crossingFirstName = fullNameParts.Length > 1 ? fullNameParts[1] : "";
            var crossingPatronymic = fullNameParts.Length > 2 ? fullNameParts[2] : "";

            if (!string.IsNullOrWhiteSpace(lastNameFilter) && !crossingLastName.StartsWith(lastNameFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(firstNameFilter) && !crossingFirstName.StartsWith(firstNameFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(patronymicFilter) && !crossingPatronymic.StartsWith(patronymicFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(citizenshipFilter) && !(crossing.Citizenship ?? string.Empty).StartsWith(citizenshipFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(passportFilter) && !crossing.PersonPassport.StartsWith(passportFilter, StringComparison.OrdinalIgnoreCase))
                return false;


            if (!string.IsNullOrEmpty(FilterCitizenship) && FilterCitizenship != "ВСЕ")
                if (!(crossing.Citizenship ?? "").Equals(FilterCitizenship, StringComparison.OrdinalIgnoreCase)) return false;

            if (!string.IsNullOrEmpty(FilterPurpose) && FilterPurpose != "ВСЕ")
                if (!(crossing.Purpose ?? "").Equals(FilterPurpose, StringComparison.OrdinalIgnoreCase)) return false;

            if (!string.IsNullOrEmpty(FilterVehicle))
                if (!crossing.VehicleInfo.Contains(FilterVehicle, StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        #region Property Change Handlers
        private async void OnCurrentPersonPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Person.LastName) ||
                e.PropertyName == nameof(Person.FirstName) ||
                e.PropertyName == nameof(Person.Patronymic) ||
                e.PropertyName == nameof(Person.Citizenship) ||
                e.PropertyName == nameof(Person.PassportData))
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

        partial void OnFilterCitizenshipChanged(string? value) => CrossingsView.Refresh();
        partial void OnFilterPurposeChanged(string? value) => CrossingsView.Refresh();
        partial void OnFilterVehicleChanged(string? value) => CrossingsView.Refresh();
        #endregion

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

            SecurityCheckStatus = "Данные не проверялись";
            SecurityCheckColor = Brushes.Transparent;

            ActiveDriverCrossing = null;

            _isShowingPreviousPassengers = false;
            _previousPassengerCrossingIds.Clear();

            ResetAllFilters();
        }

        public async Task<bool> LoadDataAsync()
        {
            try
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
                var purposes = await purposesTask;
                var citizenships = await citizenshipsTask;
                var destinations = await destinationsTask;
                var makes = await makesTask;

                Application.Current.Dispatcher.Invoke(() =>
                {
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
                    AllPurposes.Add("ВСЕ");
                    foreach (var p in purposes) AllPurposes.Add(p);

                    AllCitizenships.Clear();
                    AllCitizenships.Add("ВСЕ");
                    foreach (var c in citizenships) AllCitizenships.Add(c);

                    AllDestinations.Clear();
                    foreach (var d in destinations) AllDestinations.Add(d);

                    AllVehicleMakes.Clear();
                    foreach (var m in makes) AllVehicleMakes.Add(m);

                    FilterCitizenship = "ВСЕ";
                    FilterPurpose = "ВСЕ";
                });

                await UpdateAllDashboardStats();

                StatusMessage = $"Загружено {AllCrossings.Count(c => !c.IsDeleted)} активных записей.";
                return true;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Не удалось загрузить начальные данные: {ex.Message}\n\n{ex.StackTrace}", "Ошибка загрузки данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusMessage = "Ошибка загрузки данных.";
                });
                return false;
            }
        }

        [RelayCommand]
        private async Task RefreshData()
        {
            await LoadDataAsync();
        }

        #region Main Commands
        [RelayCommand]
        private async Task SaveCrossing()
        {
            if (SelectedCrossingType == "ПАССАЖИР" && !IsPassengerEntryMode)
            {
                MessageBox.Show("Сначала необходимо зарегистрировать водителя.\n\n" +
                                "Чтобы добавить пассажиров, сохраните пересечение для водителя, " +
                                "затем нажмите на его запись в журнале правой кнопкой мыши и выберите 'Добавить пассажира'.",
                                "Ошибка: Не выбран водитель",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

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

                    bool dobMismatch = false;
                    if (DateTime.TryParseExact(person.Dob, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dbDob))
                    {
                        if (CurrentPersonDob.HasValue)
                        {
                            dobMismatch = dbDob.Date != CurrentPersonDob.Value.Date;
                        }
                    }
                    else
                    {
                        dobMismatch = person.Dob != CurrentPerson.Dob;
                    }


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

                if (personId > 0 && !IsPassengerEntryMode)
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

                if (IsPassengerEntryMode)
                {
                    CurrentCrossing.DriverCrossingId = ActiveDriverCrossing?.ID;
                }
                else
                {
                    CurrentCrossing.DriverCrossingId = null;
                }

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

                var newCrossingFromDb = await _databaseService.GetCrossingByIdAsync(newCrossingId);
                if (newCrossingFromDb != null)
                {
                    newCrossingFromDb.IsOnWantedList = securityResult.IsOnWantedList;
                    newCrossingFromDb.IsOnWatchlist = securityResult.IsOnWatchlist;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AllCrossings.Insert(0, newCrossingFromDb);
                    });
                }

                if (!string.IsNullOrEmpty(CurrentCrossing.Purpose) && !AllPurposes.Contains(CurrentCrossing.Purpose)) Application.Current.Dispatcher.Invoke(() => AllPurposes.Add(CurrentCrossing.Purpose));
                if (!string.IsNullOrEmpty(CurrentCrossing.DestinationTown) && !AllDestinations.Contains(CurrentCrossing.DestinationTown)) Application.Current.Dispatcher.Invoke(() => AllDestinations.Add(CurrentCrossing.DestinationTown));
                if (!string.IsNullOrEmpty(CurrentVehicle.Make) && !AllVehicleMakes.Contains(CurrentVehicle.Make)) Application.Current.Dispatcher.Invoke(() => AllVehicleMakes.Add(CurrentVehicle.Make));
                if (!string.IsNullOrEmpty(CurrentPerson.Citizenship) && !AllCitizenships.Contains(CurrentPerson.Citizenship)) Application.Current.Dispatcher.Invoke(() => AllCitizenships.Add(CurrentPerson.Citizenship));

                await UpdateAllDashboardStats();

                if (IsPassengerEntryMode)
                {
                    PrepareForPassengerEntry();
                    StatusMessage = $"Пассажир для ID: {ActiveDriverCrossing?.ID} успешно сохранен. Введите следующего.";
                }
                else
                {
                    InitializeNewEntry();
                    StatusMessage = $"Пересечение ID: {newCrossingId} успешно сохранено.";
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при сохранении: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Ошибка сохранения.";
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            if (IsPassengerEntryMode)
            {
                PrepareForPassengerEntry();
            }
            else
            {
                InitializeNewEntry();
            }
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
                await LoadDataAsync();
            }
        }
        #endregion

        #region Context Menu and Filtering Commands
        [RelayCommand]
        private void ShowPersonHistory()
        {
            if (SelectedCrossing == null) return;
            ResetAllFilters(false);
            _filterPersonId = SelectedCrossing.PersonId;
            CrossingsView.Refresh();
            StatusMessage = $"Отображена история для: {SelectedCrossing.FullName}";
        }

        [RelayCommand]
        private void ResetFilter()
        {
            ResetAllFilters();
            StatusMessage = "Все фильтры сброшены.";
        }

        private void ResetAllFilters(bool refreshView = true)
        {
            _filterPersonId = null;
            _isDateFilterActive = false;

            _isShowingPreviousPassengers = false;
            _previousPassengerCrossingIds.Clear();

            if (refreshView) CrossingsView.Refresh();
        }

        [RelayCommand]
        private async Task EditPerson()
        {
            if (SelectedCrossing == null) return;

            var personToEdit = await _databaseService.GetPersonByIdAsync(SelectedCrossing.PersonId);
            if (personToEdit == null)
            {
                MessageBox.Show("Не удалось найти данные этого человека для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var editWindow = new EditPersonWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new EditPersonViewModel(_databaseService, personToEdit)
            };

            if (editWindow.ShowDialog() == true)
            {
                StatusMessage = "Данные человека обновлены. Обновление журнала...";
                await LoadDataAsync();
            }
        }

        [RelayCommand]
        private void ShowPeriodCrossings()
        {
            ResetAllFilters(false);
            _isDateFilterActive = true;
            CrossingsView.Refresh();
            StatusMessage = $"Отображены пересечения за период с {GetCombinedDateTime(DashboardStartDate, DashboardStartTime):g} по {GetCombinedDateTime(DashboardEndDate, DashboardEndTime):g}";
        }
        #endregion

        #region Proactive Security Check Command
        [RelayCommand]
        private async Task ProactiveSecurityCheck()
        {
            if (string.IsNullOrWhiteSpace(CurrentPerson.LastName) ||
                string.IsNullOrWhiteSpace(CurrentPerson.FirstName) ||
                CurrentPersonDob == null)
            {
                SecurityCheckStatus = "Заполните ФИО и дату рождения для проверки.";
                SecurityCheckColor = Brushes.LightGray;
                MessageBox.Show("Для выполнения проверки необходимо заполнить поля 'Фамилия', 'Имя' и 'Дата рождения'.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CurrentPerson.Dob = CurrentPersonDob.Value.ToString("dd.MM.yyyy");
            CurrentPerson.LastName = CurrentPerson.LastName.ToUpper();
            CurrentPerson.FirstName = CurrentPerson.FirstName.ToUpper();
            CurrentPerson.Patronymic = CurrentPerson.Patronymic?.ToUpper();

            StatusMessage = "Выполняется проверка по базам...";
            var result = await _securityService.PerformChecksAsync(CurrentPerson, true);
            StatusMessage = "Проверка завершена.";

            if (result.IsOnWantedList)
            {
                SecurityCheckStatus = "ВНИМАНИЕ: Найдено совпадение в списке РОЗЫСКА!";
                SecurityCheckColor = Brushes.Red;
                MessageBox.Show("Обнаружено совпадение в списке розыска! Рекомендуется провести дополнительную проверку.", "Лицо в розыске", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (result.IsOnWatchlist)
            {
                SecurityCheckStatus = "Внимание: Найдено совпадение в списке НАБЛЮДЕНИЯ.";
                SecurityCheckColor = Brushes.Orange;
                MessageBox.Show("Обнаружено совпадение в списке наблюдения.", "Лицо в списке наблюдения", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                SecurityCheckStatus = "Проверка пройдена. Совпадений не найдено.";
                SecurityCheckColor = Brushes.LightGreen;
                MessageBox.Show("Проверка по спискам розыска и наблюдения завершена. Совпадений не найдено.", "Проверка пройдена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Window Management and Menu Commands
        [RelayCommand]
        private void ShowGoodsWindow()
        {
            var goodsWindow = new GoodsWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new GoodsViewModel(TemporaryGoodsList)
            };
            goodsWindow.ShowDialog();
        }

        [RelayCommand]
        private async Task UpdateAllDashboardStats()
        {
            StatusMessage = "Обновление статистики...";
            var finalStartDate = GetCombinedDateTime(DashboardStartDate, DashboardStartTime);
            var finalEndDate = GetCombinedDateTime(DashboardEndDate, DashboardEndTime);

            var periodStatsTask = _databaseService.GetDashboardStatsAsync(finalStartDate, finalEndDate);
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
                catch (IOException ex)
                {
                    StatusMessage = "Ошибка доступа к файлу.";
                    MessageBox.Show($"Не удалось сохранить файл. Возможно, он открыт в другой программе или у вас нет прав на запись в эту папку.\n\nОшибка: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Ошибка при формировании отчета.";
                    MessageBox.Show($"Произошла непредвиденная ошибка: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
                catch (IOException ex)
                {
                    StatusMessage = "Ошибка доступа к файлу.";
                    MessageBox.Show($"Не удалось сохранить файл. Возможно, он открыт в другой программе или у вас нет прав на запись в эту папку.\n\nПожалуйста, попробуйте сохранить файл в другое место (например, на Рабочий стол).\n\nОшибка: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Ошибка при экспорте.";
                    MessageBox.Show($"Произошла непредвиденная ошибка во время экспорта: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void SwitchUser()
        {
            IsSwitchingUserRequested = true;
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
        private void ShowAboutInfo()
        {
            MessageBox.Show("V. 4.3 (c#), Разработчик ОПО Ленингор", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task DoubleClickOnCrossing()
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
                if (IsPassengerEntryMode && ActiveDriverCrossing != null)
                {
                    var personFromDb = await _databaseService.GetPersonByIdAsync(SelectedCrossing.PersonId);
                    if (personFromDb == null)
                    {
                        StatusMessage = "Не удалось найти данные выбранного человека.";
                        return;
                    }

                    CurrentPerson.PropertyChanged -= OnCurrentPersonPropertyChanged;
                    CurrentPerson = personFromDb;
                    CurrentPerson.PropertyChanged += OnCurrentPersonPropertyChanged;

                    if (DateTime.TryParseExact(personFromDb.Dob, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
                    {
                        CurrentPersonDob = dob;
                    }
                    else
                    {
                        CurrentPersonDob = null;
                    }

                    // --- ИСПРАВЛЕНИЕ: Явное обновление свойств для UI ---
                    CurrentCrossing.Direction = ActiveDriverCrossing.Direction;
                    CurrentCrossing.DestinationTown = ActiveDriverCrossing.DestinationTown;
                    CurrentCrossing.Purpose = ActiveDriverCrossing.Purpose;
                    OnPropertyChanged(nameof(CurrentCrossing));

                    var vehicleInfoParts = ActiveDriverCrossing.VehicleInfo.Split('/');
                    if (vehicleInfoParts.Length == 2)
                    {
                        CurrentVehicle.Make = vehicleInfoParts[0].Trim();
                        CurrentVehicle.LicensePlate = vehicleInfoParts[1].Trim();
                    }
                    OnPropertyChanged(nameof(CurrentVehicle));

                    SelectedCrossingType = "ПАССАЖИР";
                    OnPropertyChanged(nameof(IsDirectionIn));
                    OnPropertyChanged(nameof(IsDirectionOut));

                    TemporaryGoodsList.Clear();
                    SecurityCheckStatus = "Данные не проверялись";
                    SecurityCheckColor = Brushes.Transparent;

                    StatusMessage = $"Данные для пассажира {CurrentPerson.LastName} загружены. Готово к сохранению.";
                }
                else
                {
                    var personFromDb = await _databaseService.GetPersonByIdAsync(SelectedCrossing.PersonId);
                    if (personFromDb == null)
                    {
                        StatusMessage = "Не удалось найти данные выбранного человека.";
                        return;
                    }

                    CurrentPerson.PropertyChanged -= OnCurrentPersonPropertyChanged;
                    CurrentPerson = personFromDb;
                    CurrentPerson.PropertyChanged += OnCurrentPersonPropertyChanged;

                    if (DateTime.TryParseExact(personFromDb.Dob, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
                    {
                        CurrentPersonDob = dob;
                    }
                    else
                    {
                        CurrentPersonDob = null;
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
                    SecurityCheckStatus = "Данные не проверялись";
                    SecurityCheckColor = Brushes.Transparent;

                    StatusMessage = $"Данные для {CurrentPerson.LastName} загружены. Готово к созданию нового пересечения.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка при загрузке данных.";
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrepareForPassengerEntry()
        {
            if (ActiveDriverCrossing == null) return;

            if (CurrentPerson != null)
            {
                CurrentPerson.PropertyChanged -= OnCurrentPersonPropertyChanged;
            }

            CurrentPerson = new Person();
            CurrentPersonDob = null;
            TemporaryGoodsList.Clear();
            SecurityCheckStatus = "Данные не проверялись";
            SecurityCheckColor = Brushes.Transparent;
            CurrentPerson.PropertyChanged += OnCurrentPersonPropertyChanged;

            // --- ИСПРАВЛЕНИЕ: Явное обновление свойств для UI ---
            CurrentCrossing.Direction = ActiveDriverCrossing.Direction;
            CurrentCrossing.Purpose = ActiveDriverCrossing.Purpose;
            CurrentCrossing.DestinationTown = ActiveDriverCrossing.DestinationTown;
            OnPropertyChanged(nameof(CurrentCrossing));

            var vehicleInfoParts = ActiveDriverCrossing.VehicleInfo.Split('/');
            if (vehicleInfoParts.Length == 2)
            {
                CurrentVehicle.Make = vehicleInfoParts[0].Trim();
                CurrentVehicle.LicensePlate = vehicleInfoParts[1].Trim();
            }
            OnPropertyChanged(nameof(CurrentVehicle));

            SelectedCrossingType = "ПАССАЖИР";
            OnPropertyChanged(nameof(IsDirectionIn));
            OnPropertyChanged(nameof(IsDirectionOut));

            StatusMessage = $"Готово к вводу данных пассажира для ТС {ActiveDriverCrossing.VehicleInfo}";
        }

        [RelayCommand]
        private void AddPassenger()
        {
            if (!IsDriverSelected || SelectedCrossing == null) return;

            ActiveDriverCrossing = SelectedCrossing;
            PrepareForPassengerEntry();
        }



        [RelayCommand]
        private void FinishAddingPassengers()
        {
            InitializeNewEntry();
            StatusMessage = "Режим добавления пассажиров завершен.";
        }

        [RelayCommand]
        private async Task ShowPreviousPassengers()
        {
            if (ActiveDriverCrossing == null) return;

            StatusMessage = "Поиск предыдущих пассажиров...";
            try
            {
                var driverPersonId = ActiveDriverCrossing.PersonId;
                var currentCrossingId = ActiveDriverCrossing.ID;

                var previousTripId = await _databaseService.GetPreviousDriverCrossingIdAsync(driverPersonId, currentCrossingId);

                _previousPassengerCrossingIds.Clear();

                if (previousTripId.HasValue)
                {
                    var passengerCrossings = await _databaseService.GetPassengerCrossingsByDriverCrossingIdAsync(previousTripId.Value);
                    var crossingList = passengerCrossings.ToList();

                    if (crossingList.Any())
                    {
                        foreach (var c in crossingList)
                        {
                            _previousPassengerCrossingIds.Add(c.ID);
                        }
                        _isShowingPreviousPassengers = true;
                        CrossingsView.Refresh();
                        StatusMessage = $"Найдено {crossingList.Count} пассажиров из предыдущей поездки. Журнал отфильтрован.";
                    }
                    else
                    {
                        StatusMessage = "В предыдущей поездке этого водителя не найдено зарегистрированных пассажиров.";
                        _isShowingPreviousPassengers = false;
                        CrossingsView.Refresh();
                    }
                }
                else
                {
                    StatusMessage = "Не найдено предыдущих поездок для этого водителя.";
                    _isShowingPreviousPassengers = false;
                    CrossingsView.Refresh();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка при поиске пассажиров.";
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _isShowingPreviousPassengers = false;
                CrossingsView.Refresh();
            }
        }
        #endregion
    }
}

