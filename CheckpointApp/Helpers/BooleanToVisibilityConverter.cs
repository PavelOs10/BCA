using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CheckpointApp.Helpers
{
    /// <summary>
    /// Конвертирует булево значение (true/false) в значение Visibility (Visible/Collapsed).
    /// Используется в XAML для скрытия элементов на основе условий в ViewModel.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Обратное преобразование не требуется для данного приложения
            throw new NotImplementedException();
        }
    }
}
