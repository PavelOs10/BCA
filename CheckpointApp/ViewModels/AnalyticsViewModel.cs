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
using DocumentFormat.OpenXml.Wordprocessing;
using ScottPlot.WPF;

namespace CheckpointApp.ViewModels
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private List<Crossing> _crossingsData;

        // Ссылки на контролы для графиков из View
        private WpfPlot _dynamicsPlot, _citizenshipPiePlot, _topCitizenshipBarPlot, _heatmapPlot, _operatorsPlot;

        [ObservableProperty]
        private DateTime _startDate;
        [ObservableProperty]
        private DateTime _endDate;
        [ObservableProperty]
        private string _statusText;
        [ObservableProperty]
        private string _summaryText;

        public AnalyticsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            // По умолчанию ставим период за последний месяц
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
            _dynamicsPlot?.plt.Clear(); _dynamicsPlot?.Render();
            _citizenshipPiePlot?.plt.Clear(); _citizenshipPiePlot?.Render();
            _topCitizenshipBarPlot?.plt.Clear(); _topCitizenshipBarPlot?.Render();
            _heatmapPlot?.plt.Clear(); _heatmapPlot?.Render();
            _operatorsPlot?.plt.Clear(); _operatorsPlot?.Render();
        }

        private void GenerateSummary()
        {
            var totalCrossings = _crossingsData.Count;
            var uniquePersons = _crossingsData.Select(c => c.PersonId).Distinct().Count();
            var days = (EndDate - StartDate).Days + 1;
            var avgPerDay = Math.Round((double)totalCrossings / days, 1);

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
            var plt = _dynamicsPlot.plt;
            plt.Clear();

            var entries = _crossingsData.Where(c => c.Direction == "ВЪЕЗД")
                .GroupBy(c => DateTime.Parse(c.Timestamp).Date)
                .ToDictionary(g => g.Key, g => (double)g.Count());

            var exits = _crossingsData.Where(c => c.Direction == "ВЫЕЗД")
                .GroupBy(c => DateTime.Parse(c.Timestamp).Date)
                .ToDictionary(g => g.Key, g => (double)g.Count());

            var allDates = entries.Keys.Union(exits.Keys).OrderBy(d => d).ToArray();
            if (!allDates.Any()) return;

            var entryValues = allDates.Select(d => entries.ContainsKey(d) ? entries[d] : 0).ToArray();
            var exitValues = allDates.Select(d => exits.ContainsKey(d) ? exits[d] : 0).ToArray();
            var datePositions = allDates.Select(d => d.ToOADate()).ToArray();

            plt.PlotBar(datePositions, entryValues, label: "Въезд", barWidth: 0.4, xOffset: -0.2);
            plt.PlotBar(datePositions, exitValues, label: "Выезд", barWidth: 0.4, xOffset: 0.2);

            plt.XAxis.DateTimeFormat(true);
            plt.Title("Динамика пересечений по дням");
            plt.Legend(location: legendLocation.upperRight);
            _dynamicsPlot.Render();
        }

        private void GenerateGeographyPlots()
        {
            var byCitizenship = _crossingsData
                .GroupBy(c => c.Citizenship ?? "НЕ УКАЗАНО")
                .Select(g => new { Citizenship = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Круговая диаграмма (топ 5 + остальные)
            var pltPie = _citizenshipPiePlot.plt;
            pltPie.Clear();
            var top5 = byCitizenship.Take(5).ToList();
            var othersCount = byCitizenship.Skip(5).Sum(x => x.Count);

            var values = top5.Select(x => (double)x.Count).ToList();
            var labels = top5.Select(x => x.Citizenship).ToList();
            if (othersCount > 0)
            {
                values.Add(othersCount);
                labels.Add("ДРУГИЕ");
            }
            pltPie.PlotPie(values.ToArray(), labels.ToArray(), showPercentages: true);
            pltPie.Title("Распределение по гражданству");
            _citizenshipPiePlot.Render();

            // Столбчатая диаграмма (топ 15)
            var pltBar = _topCitizenshipBarPlot.plt;
            pltBar.Clear();
            var top15 = byCitizenship.Take(15).Reverse().ToList();
            var barValues = top15.Select(x => (double)x.Count).ToArray();
            var barLabels = top15.Select(x => x.Citizenship).ToArray();
            var yPositions = Enumerable.Range(0, top15.Count).Select(i => (double)i).ToArray();

            pltBar.PlotBarH(yPositions, barValues);
            pltBar.YTicks(yPositions, barLabels);
            pltBar.Title("Топ-15 национальностей");
            _topCitizenshipBarPlot.Render();
        }

        private void GenerateHeatmapPlot()
        {
            var plt = _heatmapPlot.plt;
            plt.Clear();

            double[,] intensities = new double[7, 24]; // 7 дней недели, 24 часа
            foreach (var crossing in _crossingsData)
            {
                var dt = DateTime.Parse(crossing.Timestamp);
                int dayOfWeek = ((int)dt.DayOfWeek + 6) % 7; // Пн=0, Вс=6
                int hour = dt.Hour;
                intensities[dayOfWeek, hour]++;
            }

            plt.PlotHeatmap(intensities, lockScales: false);
            plt.YAxis.Label("День недели");
            plt.XAxis.Label("Час дня");
            plt.YTicks(new double[] { 0, 1, 2, 3, 4, 5, 6 }, new string[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" });
            plt.Title("Тепловая карта нагрузки по времени");
            _heatmapPlot.Render();
        }

        private void GenerateOperatorsPlot()
        {
            var plt = _operatorsPlot.plt;
            plt.Clear();

            var byOperator = _crossingsData
                .GroupBy(c => c.OperatorUsername ?? "N/A")
                .Select(g => new { Operator = g.Key, Count = (double)g.Count() })
                .OrderBy(x => x.Count)
                .ToList();

            var values = byOperator.Select(x => x.Count).ToArray();
            var labels = byOperator.Select(x => x.Operator).ToArray();
            var positions = Enumerable.Range(0, byOperator.Count).Select(i => (double)i).ToArray();

            plt.PlotBarH(positions, values);
            plt.YTicks(positions, labels);
            plt.Title("Рейтинг операторов по количеству обработанных пересечений");
            _operatorsPlot.Render();
        }
    }
}
