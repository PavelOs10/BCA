using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.DataAccess;
using CheckpointApp.Models;
using ScottPlot;
using ScottPlot.WPF;

namespace CheckpointApp.ViewModels
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private List<Crossing> _crossingsData;

        private WpfPlot? _dynamicsPlot, _citizenshipPiePlot, _topCitizenshipBarPlot, _heatmapPlot, _operatorsPlot;

        [ObservableProperty]
        private DateTime _startDate;
        [ObservableProperty]
        private DateTime _endDate;
        [ObservableProperty]
        private string _statusText = string.Empty;
        [ObservableProperty]
        private string _summaryText = "Выберите период и сформируйте отчет.";

        public AnalyticsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            EndDate = DateTime.Today;
            StartDate = EndDate.AddMonths(-1);
            _crossingsData = new List<Crossing>();
        }

        public void InitializePlots(WpfPlot dyn, WpfPlot pie, WpfPlot bar, WpfPlot heat, WpfPlot ops)
        {
            _dynamicsPlot = dyn;
            _citizenshipPiePlot = pie;
            _topCitizenshipBarPlot = bar;
            _heatmapPlot = heat;
            _operatorsPlot = ops;
        }

        [RelayCommand]
        private async Task GenerateReport()
        {
            StatusText = "Загрузка данных...";
            _crossingsData = (await _databaseService.GetCrossingsByDateRangeAsync(StartDate, EndDate)).ToList();
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
            _dynamicsPlot?.Plot.Clear(); _dynamicsPlot?.Refresh();
            _citizenshipPiePlot?.Plot.Clear(); _citizenshipPiePlot?.Refresh();
            _topCitizenshipBarPlot?.Plot.Clear(); _topCitizenshipBarPlot?.Refresh();
            _heatmapPlot?.Plot.Clear(); _heatmapPlot?.Refresh();
            _operatorsPlot?.Plot.Clear(); _operatorsPlot?.Refresh();
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
            if (_dynamicsPlot == null) return;
            var plot = _dynamicsPlot.Plot;
            plot.Clear();

            var entries = _crossingsData.Where(c => c.Direction == "ВЪЕЗД")
                .GroupBy(c => DateTime.Parse(c.Timestamp).Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var exits = _crossingsData.Where(c => c.Direction == "ВЫЕЗД")
                .GroupBy(c => DateTime.Parse(c.Timestamp).Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var allDates = entries.Keys.Union(exits.Keys).OrderBy(d => d).ToArray();
            if (!allDates.Any()) return;

            var entryValues = allDates.Select(d => (double)(entries.ContainsKey(d) ? entries[d] : 0)).ToArray();
            var exitValues = allDates.Select(d => (double)(exits.ContainsKey(d) ? exits[d] : 0)).ToArray();

            plot.Add.Bars(entryValues).Label = "Въезд";
            plot.Add.Bars(exitValues).Label = "Выезд";

            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
            plot.Title("Динамика пересечений по дням");
            plot.Legend.IsVisible = true;
            plot.Legend.Location = Alignment.UpperRight;
            _dynamicsPlot.Refresh();
        }

        private void GenerateGeographyPlots()
        {
            if (_citizenshipPiePlot == null || _topCitizenshipBarPlot == null) return;
            var byCitizenship = _crossingsData
                .GroupBy(c => c.Citizenship ?? "НЕ УКАЗАНО")
                .Select(g => new { Citizenship = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Круговая диаграмма
            var piePlot = _citizenshipPiePlot.Plot;
            piePlot.Clear();
            var top5 = byCitizenship.Take(5).ToList();
            var othersCount = byCitizenship.Skip(5).Sum(x => x.Count);

            List<PieSlice> slices = new();
            foreach (var item in top5)
            {
                slices.Add(new PieSlice() { Value = item.Count, Label = item.Citizenship });
            }
            if (othersCount > 0)
            {
                slices.Add(new PieSlice() { Value = othersCount, Label = "ДРУГИЕ" });
            }
            piePlot.Add.Pie(slices);
            piePlot.Title("Распределение по гражданству");
            _citizenshipPiePlot.Refresh();

            // Столбчатая диаграмма
            var barPlot = _topCitizenshipBarPlot.Plot;
            barPlot.Clear();
            var top15 = byCitizenship.Take(15).Reverse().ToList();
            var barValues = top15.Select(x => (double)x.Count).ToArray();

            var bars = barPlot.Add.Bars(barValues);
            // --- ИСПРАВЛЕНО: Orientation заменено на Horizontal ---
            bars.Horizontal = true;

            var ticks = top15.Select((item, index) => new Tick(index, item.Citizenship)).ToArray();
            barPlot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
            barPlot.Axes.Left.MajorTickStyle.Length = 0;
            barPlot.Title("Топ-15 национальностей");
            _topCitizenshipBarPlot.Refresh();
        }

        private void GenerateHeatmapPlot()
        {
            if (_heatmapPlot == null) return;
            var plot = _heatmapPlot.Plot;
            plot.Clear();

            double[,] intensities = new double[7, 24];
            foreach (var crossing in _crossingsData)
            {
                var dt = DateTime.Parse(crossing.Timestamp);
                int dayOfWeek = ((int)dt.DayOfWeek + 6) % 7;
                int hour = dt.Hour;
                intensities[dayOfWeek, hour]++;
            }

            plot.Add.Heatmap(intensities);
            var yTicks = new Tick[]
            {
                new (0, "Пн"), new (1, "Вт"), new (2, "Ср"), new (3, "Чт"),
                new (4, "Пт"), new (5, "Сб"), new (6, "Вс")
            };
            plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(yTicks);
            plot.Title("Тепловая карта нагрузки по времени");
            _heatmapPlot.Refresh();
        }

        private void GenerateOperatorsPlot()
        {
            if (_operatorsPlot == null) return;
            var plot = _operatorsPlot.Plot;
            plot.Clear();

            var byOperator = _crossingsData
                .GroupBy(c => c.OperatorUsername ?? "N/A")
                .Select(g => new { Operator = g.Key, Count = g.Count() })
                .OrderBy(x => x.Count)
                .ToList();

            var values = byOperator.Select(x => (double)x.Count).ToArray();

            var bars = plot.Add.Bars(values);
            // --- ИСПРАВЛЕНО: Orientation заменено на Horizontal ---
            bars.Horizontal = true;

            var ticks = byOperator.Select((item, index) => new Tick(index, item.Operator)).ToArray();
            plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
            plot.Axes.Left.MajorTickStyle.Length = 0;
            plot.Title("Рейтинг операторов");
            _operatorsPlot.Refresh();
        }
    }
}
