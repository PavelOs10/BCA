using System.Windows;

namespace CheckpointApp.Views
{
    public partial class GoodsWindow : Window
    {
        public GoodsWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
