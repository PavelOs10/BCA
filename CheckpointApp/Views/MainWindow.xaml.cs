using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CheckpointApp.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Debug.WriteLine("--- Конструктор MainWindow вызван ---");
            this.Loaded += (s, e) => Debug.WriteLine("--- Событие MainWindow.Loaded сработало ---");
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            Debug.WriteLine("--- Событие MainWindow_Closing сработало ---");
            Debug.WriteLine($"Окно активно: {this.IsActive}");

            // --- ГЛАВНОЕ ИЗМЕНЕНИЕ: ВЫВОД СТЕКА ВЫЗОВОВ ---
            // Эта строка покажет нам, какой код вызвал закрытие окна.
            Debug.WriteLine("Трассировка стека при закрытии:");
            Debug.WriteLine(new StackTrace());
            // ----------------------------------------------------
        }
    }
}

