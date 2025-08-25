using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using System.Globalization;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.Legends;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;

namespace CheckpointApp.ViewModels
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private List<Crossing> _crossingsData;
        private List<GoodReportItem> _goodsData;

        [ObservableProperty] private DateTime _startDate;
        [ObservableProperty] private DateTime _endDate;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _summaryText = "Выберите период и сформируйте отчет.";

        public List<string> GroupingOptions { get; } = new List<string> { "По дням", "По неделям", "По месяцам" };
        [ObservableProperty] private string _selectedGroupingOption;

        [ObservableProperty] private PlotModel _dynamicsModel;
        [ObservableProperty] private PlotModel _geographyModel;
        [ObservableProperty] private PlotModel _heatmapModel;
        [ObservableProperty] private PlotModel _operatorsModel;
        [ObservableProperty] private PlotModel _goodsModel;

        // --- НОВЫЕ СВОЙСТВА ДЛЯ АНАЛИЗА ПО ЛИЦУ ---
        [ObservableProperty]
        private ObservableCollection<Person> _allPersons;
        public ICollectionView PersonsView { get; }
        [ObservableProperty]
        private string _personSearchText = string.Empty;
        [ObservableProperty]
        private Person? _selectedPersonForReport;
        [ObservableProperty]
        private ObservableCollection<PersonGoodsItem> _personGoodsHistory;
        [ObservableProperty]
        private ObservableCollection<GoodReportItem> _personGoodsSummary;


        public AnalyticsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            EndDate = DateTime.Today;
            StartDate = EndDate.AddMonths(-1);
            _selectedGroupingOption = GroupingOptions[0];
            _crossingsData = new List<Crossing>();
            _goodsData = new List<GoodReportItem>();

            _dynamicsModel = new PlotModel { Title = "Динамика пересечений" };
            _geographyModel = new PlotModel { Title = "Распределение по гражданству" };
            _heatmapModel = new PlotModel { Title = "Нагрузка по времени (День недели / Час)" };
            _operatorsModel = new PlotModel { Title = "Активность операторов" };
            _goodsModel = new PlotModel { Title = "Топ-10 товаров по количеству" };

            // --- ИНИЦИАЛИЗАЦИЯ НОВЫХ СВОЙСТВ ---
            _allPersons = new ObservableCollection<Person>();
            PersonsView = CollectionViewSource.GetDefaultView(_allPersons);
            PersonsView.Filter = FilterPersons;
            _personGoodsHistory = new ObservableCollection<PersonGoodsItem>();
            _personGoodsSummary = new ObservableCollection<GoodReportItem>();

            // Загружаем список людей один раз при создании
            _ = LoadAllPersonsAsync();
        }

        private async Task LoadAllPersonsAsync()
        {
            var personList = await _databaseService.GetAllPersonsAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                AllPersons.Clear();
                foreach (var p in personList)
                {
                    AllPersons.Add(p);
                }
            });
        }

        [RelayCommand]
        private async Task GenerateReport()
        {
            StatusText = "Загрузка данных...";
            var crossingsTask = _databaseService.GetCrossingsByDateRangeAsync(StartDate, EndDate.AddDays(1));
            var goodsTask = _databaseService.GetGoodsByDateRangeAsync(StartDate, EndDate.AddDays(1));

            await Task.WhenAll(crossingsTask, goodsTask);

            _crossingsData = (await crossingsTask).ToList();
            _goodsData = (await goodsTask).ToList();

            StatusText = $"Данные загружены. Обработано {_crossingsData.Count} пересечений и {_goodsData.Count} видов товаров.";

            if (!_crossingsData.Any())
            {
                ClearAllPlots();
                SummaryText = "За выбранный период нет данных о пересечениях.";
                return;
            }

            GenerateSummary();
            GenerateDynamicsPlot();
            GenerateGeographyPlots();
            GenerateHeatmapPlot();
            GenerateOperatorsPlot();
            GenerateGoodsPlot();
        }

        private void ClearAllPlots()
        {
            DynamicsModel = new PlotModel { Title = "Динамика пересечений" };
            GeographyModel = new PlotModel { Title = "Распределение по гражданству" };
            HeatmapModel = new PlotModel { Title = "Нагрузка по времени (День недели / Час)" };
            OperatorsModel = new PlotModel { Title = "Активность операторов" };
            GoodsModel = new PlotModel { Title = "Топ-10 товаров по количеству" };
            OnPropertyChanged(nameof(DynamicsModel));
            OnPropertyChanged(nameof(GeographyModel));
            OnPropertyChanged(nameof(HeatmapModel));
            OnPropertyChanged(nameof(OperatorsModel));
            OnPropertyChanged(nameof(GoodsModel));
        }

        private void GenerateSummary()
        {
            var totalCrossings = _crossingsData.Count;
            var uniquePersons = _crossingsData.Select(c => c.PersonId).Distinct().Count();
            var days = (EndDate - StartDate).Days + 1;
            var avgPerDay = (days > 0) ? Math.Round((double)totalCrossings / days, 1) : totalCrossings;

            var peakDay = _crossingsData
                .GroupBy(c => DateTime.Parse(c.Timestamp).Date)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            var sb = new StringBuilder();
            sb.AppendLine($"Анализ за период: с {StartDate:dd.MM.yyyy} по {EndDate:dd.MM.yyyy}\n");
            sb.AppendLine($"- Общее количество пересечений: {totalCrossings}");
            sb.AppendLine($"- Уникальных лиц: {uniquePersons}");
            sb.AppendLine($"- Среднесуточное количество пересечений: {avgPerDay}");
            if (peakDay != null)
            {
                sb.AppendLine($"- Пиковый день: {peakDay.Key:dd.MM.yyyy} ({peakDay.Count()} пересечений)");
            }
            SummaryText = sb.ToString();
        }

        private void GenerateDynamicsPlot()
        {
            var model = new PlotModel { Title = "Динамика пересечений" };
            model.Legends.Add(new Legend
            {
                LegendTitle = "Обозначения",
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Outside
            });

            var groupedData = _crossingsData.GroupBy(c =>
            {
                var date = DateTime.Parse(c.Timestamp).Date;
                return SelectedGroupingOption switch
                {
                    "По неделям" => "Н" + CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday) + "-" + date.Year,
                    "По месяцам" => date.ToString("MMMM yyyy", new CultureInfo("ru-RU")),
                    _ => date.ToString("dd.MM.yyyy")
                };
            }).Select(g => new
            {
                Period = g.Key,
                Total = g.Count(),
                Entered = g.Count(x => x.Direction == "ВЪЕЗД"),
                Exited = g.Count(x => x.Direction == "ВЫЕЗД")
            }).ToList();

            var seriesTotal = new BarSeries { Title = "Всего пересекло", StrokeThickness = 1 };
            var seriesEntered = new BarSeries { Title = "Въехало", StrokeThickness = 1 };
            var seriesExited = new BarSeries { Title = "Выехало", StrokeThickness = 1 };

            foreach (var data in groupedData)
            {
                seriesTotal.Items.Add(new BarItem(data.Total));
                seriesEntered.Items.Add(new BarItem(data.Entered));
                seriesExited.Items.Add(new BarItem(data.Exited));
            }

            model.Series.Add(seriesTotal);
            model.Series.Add(seriesEntered);
            model.Series.Add(seriesExited);

            model.Axes.Add(new CategoryAxis { Position = AxisPosition.Left, ItemsSource = groupedData.Select(d => d.Period).ToList() });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0 });

            DynamicsModel = model;
        }

        private void GenerateGeographyPlots()
        {
            var model = new PlotModel { Title = "Распределение по гражданству" };
            var byCitizenship = _crossingsData
                .GroupBy(c => c.Citizenship ?? "НЕ УКАЗАНО")
                .Select(g => new { Citizenship = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var pieSeries = new PieSeries
            {
                StrokeThickness = 2.0,
                InsideLabelPosition = 0.8,
                AngleSpan = 360,
                StartAngle = 0
            };

            foreach (var item in byCitizenship)
            {
                pieSeries.Slices.Add(new PieSlice(item.Citizenship, item.Count) { IsExploded = false });
            }

            model.Series.Add(pieSeries);
            GeographyModel = model;
        }

        private void GenerateHeatmapPlot()
        {
            var model = new PlotModel { Title = "Нагрузка по времени (День недели / Час)" };
            double[,] intensities = new double[7, 24];
            foreach (var crossing in _crossingsData)
            {
                var dt = DateTime.Parse(crossing.Timestamp);
                int dayOfWeek = ((int)dt.DayOfWeek + 6) % 7;
                int hour = dt.Hour;
                intensities[dayOfWeek, hour]++;
            }

            var heatMapSeries = new HeatMapSeries
            {
                X0 = 0,
                X1 = 23,
                Y0 = 0,
                Y1 = 6,
                Data = intensities,
                Interpolate = false
            };

            model.Series.Add(heatMapSeries);
            model.Axes.Add(new LinearColorAxis { Position = AxisPosition.Right, Palette = OxyPalettes.Jet(200) });
            model.Axes.Add(new CategoryAxis { Position = AxisPosition.Bottom, Title = "Час дня", ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString("D2")).ToList() });
            model.Axes.Add(new CategoryAxis { Position = AxisPosition.Left, Title = "День недели", ItemsSource = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" } });

            HeatmapModel = model;
        }

        private void GenerateOperatorsPlot()
        {
            var model = new PlotModel { Title = "Активность операторов" };
            var byOperator = _crossingsData
                .GroupBy(c => c.OperatorUsername ?? "N/A")
                .Select(g => new {
                    Operator = g.Key,
                    Total = g.Count()
                })
                .OrderBy(x => x.Operator)
                .ToList();

            var barSeries = new BarSeries { Title = "Обработано пересечений", StrokeThickness = 1 };
            foreach (var op in byOperator)
            {
                barSeries.Items.Add(new BarItem(op.Total));
            }

            model.Series.Add(barSeries);
            model.Axes.Add(new CategoryAxis { Position = AxisPosition.Left, ItemsSource = byOperator.Select(op => op.Operator).ToList() });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0 });

            OperatorsModel = model;
        }

        private void GenerateGoodsPlot()
        {
            var model = new PlotModel { Title = "Топ-10 товаров по количеству" };
            if (!_goodsData.Any())
            {
                model.Subtitle = "Нет данных о товарах за выбранный период.";
                GoodsModel = model;
                return;
            }

            var topGoods = _goodsData.Take(10).ToList();

            var barSeries = new BarSeries
            {
                Title = "Общее количество",
                StrokeThickness = 1,
                LabelFormatString = "{0:0.##}"
            };

            foreach (var good in topGoods)
            {
                barSeries.Items.Add(new BarItem(good.TotalQuantity));
            }

            model.Series.Add(barSeries);
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                ItemsSource = topGoods.Select(g => $"{g.Description} ({g.Unit})").ToList(),
                Angle = topGoods.Any(g => g.Description.Length > 20) ? -30 : 0
            };
            model.Axes.Add(categoryAxis);
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0, Title = "Количество" });

            GoodsModel = model;
        }

        #region Person Goods Analysis Logic

        private bool FilterPersons(object obj)
        {
            if (string.IsNullOrWhiteSpace(PersonSearchText))
                return true;

            if (obj is Person person)
            {
                return person.FullName.Contains(PersonSearchText, StringComparison.OrdinalIgnoreCase) ||
                       person.PassportData.Contains(PersonSearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        partial void OnPersonSearchTextChanged(string value)
        {
            PersonsView.Refresh();
        }

        [RelayCommand]
        private async Task GeneratePersonGoodsReport()
        {
            if (SelectedPersonForReport == null)
            {
                MessageBox.Show("Пожалуйста, выберите человека из списка.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusText = $"Загрузка данных для {SelectedPersonForReport.LastName}...";
            var history = await _databaseService.GetGoodsByPersonIdAndDateRangeAsync(SelectedPersonForReport.Id, StartDate, EndDate.AddDays(1));
            var historyList = history.ToList();

            PersonGoodsHistory.Clear();
            foreach (var item in historyList)
            {
                PersonGoodsHistory.Add(item);
            }

            var summary = historyList
                .GroupBy(h => new { h.Description, h.Unit })
                .Select(g => new GoodReportItem
                {
                    Description = g.Key.Description,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(item => item.Quantity)
                })
                .OrderByDescending(s => s.TotalQuantity)
                .ToList();

            PersonGoodsSummary.Clear();
            foreach (var item in summary)
            {
                PersonGoodsSummary.Add(item);
            }

            StatusText = $"Отчет для {SelectedPersonForReport.LastName} сформирован. Найдено {historyList.Count} записей.";
        }

        #endregion
    }
}
