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
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.Drawing;

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

        [ObservableProperty] private ISeries[] _dynamicsSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _dynamicsXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _geographySeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] _heatmapSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _heatmapXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _heatmapYAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _operatorsSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _operatorsYAxes = Array.Empty<Axis>();


        public AnalyticsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            EndDate = DateTime.Today;
            StartDate = EndDate.AddMonths(-1);
            _selectedGroupingOption = GroupingOptions[0];
            _crossingsData = new List<Crossing>();
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
            DynamicsSeries = Array.Empty<ISeries>();
            GeographySeries = Array.Empty<ISeries>();
            HeatmapSeries = Array.Empty<ISeries>();
            OperatorsSeries = Array.Empty<ISeries>();
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

            DynamicsSeries = new ISeries[]
            {
                new ColumnSeries<int> { Name = "Всего пересекло", Values = groupedData.Select(d => d.Total).ToList() },
                new ColumnSeries<int> { Name = "Въехало", Values = groupedData.Select(d => d.Entered).ToList() },
                new ColumnSeries<int> { Name = "Выехало", Values = groupedData.Select(d => d.Exited).ToList() }
            };

            DynamicsXAxes = new Axis[]
            {
                new Axis { Labels = groupedData.Select(d => d.Period).ToList(), LabelsRotation = 45 }
            };
        }

        private void GenerateGeographyPlots()
        {
            var totalCrossings = _crossingsData.Count;
            var byCitizenship = _crossingsData
                .GroupBy(c => c.Citizenship ?? "НЕ УКАЗАНО")
                .Select(g => new { Citizenship = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            GeographySeries = byCitizenship.Select(item => new PieSeries<int>
            {
                Name = item.Citizenship,
                Values = new[] { item.Count },
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = PolarLabelsPosition.Outer,
                DataLabelsFormatter = p => $"{p.Model} ({(double)p.Model / totalCrossings:P1})"
            }).ToArray();
        }

        private void GenerateHeatmapPlot()
        {
            double[,] intensities = new double[7, 24];
            foreach (var crossing in _crossingsData)
            {
                var dt = DateTime.Parse(crossing.Timestamp);
                int dayOfWeek = ((int)dt.DayOfWeek + 6) % 7;
                int hour = dt.Hour;
                intensities[dayOfWeek, hour]++;
            }

            var points = new List<WeightedPoint>();
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 24; j++)
                {
                    points.Add(new WeightedPoint(j, i, intensities[i, j]));
                }
            }

            HeatmapSeries = new ISeries[]
            {
                new HeatSeries<WeightedPoint>
                {
                    Values = points,
                    ColorStops = new double[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0 },
                    HeatMap = new LvcColor[]
                    {
                        SKColors.AliceBlue.AsLvcColor(), SKColors.LightSkyBlue.AsLvcColor(), SKColors.CornflowerBlue.AsLvcColor(),
                        SKColors.RoyalBlue.AsLvcColor(), SKColors.MediumBlue.AsLvcColor(), SKColors.DarkBlue.AsLvcColor(), SKColors.Navy.AsLvcColor()
                    }
                }
            };

            HeatmapXAxes = new[] { new Axis { Name = "Час дня" } };
            HeatmapYAxes = new[] { new Axis { Name = "День недели", Labels = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" } } };
        }

        private void GenerateOperatorsPlot()
        {
            var byOperator = _crossingsData
                .GroupBy(c => c.OperatorUsername ?? "N/A")
                .Select(g => new {
                    Operator = g.Key,
                    Total = g.Count(),
                    Entered = g.Count(x => x.Direction == "ВЪЕЗД"),
                    Exited = g.Count(x => x.Direction == "ВЫЕЗД")
                })
                .OrderBy(x => x.Operator)
                .ToList();

            OperatorsSeries = new ISeries[]
            {
                new RowSeries<int> { Name = "Всего", Values = byOperator.Select(op => op.Total).ToList() },
                new RowSeries<int> { Name = "Въехало", Values = byOperator.Select(op => op.Entered).ToList() },
                new RowSeries<int> { Name = "Выехало", Values = byOperator.Select(op => op.Exited).ToList() }
            };

            OperatorsYAxes = new Axis[]
            {
                new Axis { Labels = byOperator.Select(op => op.Operator).ToList() }
            };
        }
    }
}
