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
// --- ЗАМЕНА: Добавлены using для OxyPlot ---
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.Legends;

namespace CheckpointApp.ViewModels
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private List<Crossing> _crossingsData;

        [ObservableProperty]
        private DateTime _startDate;
        [ObservableProperty]
        private DateTime _endDate;
        [ObservableProperty]
        private string _statusText = string.Empty;
        [ObservableProperty]
        private string _summaryText = "Выберите период и сформируйте отчет.";

        public List<string> GroupingOptions { get; } = new List<string> { "По дням", "По неделям", "По месяцам" };
        [ObservableProperty]
        private string _selectedGroupingOption;

        // --- ЗАМЕНА: Свойства LiveCharts заменены на PlotModel из OxyPlot ---
        [ObservableProperty] private PlotModel _dynamicsModel;
        [ObservableProperty] private PlotModel _geographyModel;
        [ObservableProperty] private PlotModel _heatmapModel;
        [ObservableProperty] private PlotModel _operatorsModel;


        public AnalyticsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            EndDate = DateTime.Today;
            StartDate = EndDate.AddMonths(-1);
            _selectedGroupingOption = GroupingOptions[0];
            _crossingsData = new List<Crossing>();

            // Инициализация моделей для графиков
            _dynamicsModel = new PlotModel { Title = "Динамика пересечений" };
            _geographyModel = new PlotModel { Title = "Распределение по гражданству" };
            _heatmapModel = new PlotModel { Title = "Нагрузка по времени (День недели / Час)" };
            _operatorsModel = new PlotModel { Title = "Активность операторов" };
        }

        [RelayCommand]
        private async Task GenerateReport()
        {
            StatusText = "Загрузка данных...";
            _crossingsData = (await _databaseService.GetCrossingsByDateRangeAsync(StartDate, EndDate.AddDays(1))).ToList();
            StatusText = $"Данные загружены. Обработано {_crossingsData.Count} записей.";

            if (!_crossingsData.Any())
            {
                ClearAllPlots();
                SummaryText = "За выбранный период нет данных.";
                return;
            }

            GenerateSummary();
            GenerateDynamicsPlot();
            GenerateGeographyPlots();
            GenerateHeatmapPlot();
            GenerateOperatorsPlot();
        }

        private void ClearAllPlots()
        {
            DynamicsModel = new PlotModel { Title = "Динамика пересечений" };
            GeographyModel = new PlotModel { Title = "Распределение по гражданству" };
            HeatmapModel = new PlotModel { Title = "Нагрузка по времени (День недели / Час)" };
            OperatorsModel = new PlotModel { Title = "Активность операторов" };
            // Принудительное обновление UI
            OnPropertyChanged(nameof(DynamicsModel));
            OnPropertyChanged(nameof(GeographyModel));
            OnPropertyChanged(nameof(HeatmapModel));
            OnPropertyChanged(nameof(OperatorsModel));
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

            // --- ИСПРАВЛЕНИЕ: Легенда настраивается через отдельный объект Legend ---
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
    }
}
