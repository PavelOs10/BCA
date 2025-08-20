using System.Collections.ObjectModel;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckpointApp.Models;

namespace CheckpointApp.ViewModels
{
    public partial class GoodsViewModel : ObservableObject
    {
        // Работаем напрямую с коллекцией из MainViewModel
        public ObservableCollection<TempGood> GoodsList { get; }

        // Свойства для полей ввода
        [ObservableProperty]
        private string _newGoodDescription;
        [ObservableProperty]
        private double _newGoodQuantity = 1.0;
        [ObservableProperty]
        private string _newGoodUnit = "ШТ";

        // Свойство для удаления
        [ObservableProperty]
        private TempGood _selectedGood;

        // Предзаполненный список единиц измерения
        public List<string> CommonUnits { get; } = new List<string> { "ШТ", "КГ", "Л", "М3", "УПАК" };

        public GoodsViewModel(ObservableCollection<TempGood> goods)
        {
            GoodsList = goods;
        }

        [RelayCommand]
        private void AddGood()
        {
            if (string.IsNullOrWhiteSpace(NewGoodDescription) || NewGoodQuantity <= 0)
            {
                return;
            }

            var newGood = new TempGood
            {
                Description = NewGoodDescription.ToUpper(),
                Quantity = NewGoodQuantity,
                Unit = NewGoodUnit?.ToUpper() ?? "ШТ"
            };

            GoodsList.Add(newGood);

            // Очищаем поля для следующего ввода
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
        }
    }
}
