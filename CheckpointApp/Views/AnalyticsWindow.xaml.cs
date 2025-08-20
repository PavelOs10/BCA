using System.Windows;
using CheckpointApp.ViewModels;

namespace CheckpointApp.Views
{
    public partial class AnalyticsWindow : Window
    {
        public AnalyticsWindow()
        {
            InitializeComponent();
            // Передаем контролы для графиков в ViewModel
            if (DataContext is AnalyticsViewModel vm)
            {
                vm.InitializePlots(DynamicsPlot, CitizenshipPiePlot, TopCitizenshipBarPlot, HeatmapPlot, OperatorsPlot);
            }
        }
    }
}
