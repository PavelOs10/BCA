using System.Collections.ObjectModel;
using System.Windows;
using CheckpointApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckpointApp.ViewModels
{
    public partial class GoodsViewModel : ObservableObject
    {
        // Ссылка на временный список товаров из MainViewModel
        public ObservableCollection<TempGood> GoodsList { get; }

        public ObservableCollection<string> CommonUnits { get; }

        [ObservableProperty]
        private string _newGoodDescription = string.Empty;

        [ObservableProperty]
        private double _newGoodQuantity = 1.0;

        [ObservableProperty]
        private string _newGoodUnit = "ШТ";

        [ObservableProperty]
        private TempGood? _selectedGood;

        public GoodsViewModel(ObservableCollection<TempGood> temporaryGoodsList)
        {
            GoodsList = temporaryGoodsList;
            CommonUnits = new ObservableCollection<string> { "ШТ", "КГ", "Л", "М3", "УПАК" };
        }

        [RelayCommand]
        private void AddGood()
        {
            if (string.IsNullOrWhiteSpace(NewGoodDescription) || NewGoodQuantity <= 0)
            {
                MessageBox.Show("Наименование не может быть пустым, а количество должно быть больше нуля.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newGood = new TempGood
            {
                Description = NewGoodDescription,
                Quantity = NewGoodQuantity,
                Unit = NewGoodUnit
            };

            GoodsList.Add(newGood);

            // Очистка полей для нового ввода
            NewGoodDescription = string.Empty;
            NewGoodQuantity = 1.0;
        }

        [RelayCommand]
        private void RemoveGood()
        {
            if (SelectedGood != null)
            {
                GoodsList.Remove(SelectedGood);
            }
            else
            {
                MessageBox.Show("Выберите товар для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
