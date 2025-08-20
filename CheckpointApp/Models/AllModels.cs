using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace CheckpointApp.Models
{
    // Модель пользователя системы
    public class User
    {
        public int ID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Модель физического лица (теперь наследуется от ObservableObject для живого поиска)
    public partial class Person : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _lastName = string.Empty;
        [ObservableProperty] private string _firstName = string.Empty;
        [ObservableProperty] private string? _patronymic;
        [ObservableProperty] private string _dob = string.Empty;
        [ObservableProperty] private string _citizenship = string.Empty;
        [ObservableProperty] private string _passportData = string.Empty;
        [ObservableProperty] private string? _notes;
    }

    // Модель транспортного средства
    public class Vehicle
    {
        public int ID { get; set; }
        public string Make { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
    }

    // Модель события пересечения границы
    public class Crossing
    {
        public int ID { get; set; }
        public int PersonId { get; set; }
        public int? VehicleId { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string? DestinationTown { get; set; }
        public string CrossingType { get; set; } = string.Empty;
        public int OperatorId { get; set; }
        public string Timestamp { get; set; } = string.Empty;

        // Свойства для отображения и аналитики
        public string FullName { get; set; } = string.Empty;
        public string PersonDob { get; set; } = string.Empty;
        public string PersonPassport { get; set; } = string.Empty;
        public string VehicleInfo { get; set; } = string.Empty;
        public string OperatorUsername { get; set; } = string.Empty;
        public string? Citizenship { get; set; } // <-- ИСПРАВЛЕНО: Добавлено недостающее поле
    }

    // Модель товара/груза
    public class Good
    {
        public int ID { get; set; }
        public int CrossingId { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    // Модель для временного хранения товаров
    public class TempGood
    {
        public string Description { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    // Модель лица в списке розыска
    public class WantedPerson
    {
        public int ID { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? Patronymic { get; set; }
        public string Dob { get; set; } = string.Empty;
        public string Info { get; set; } = string.Empty;
        public string Actions { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // Модель лица в списке наблюдения
    public class WatchlistPerson
    {
        public int ID { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? Patronymic { get; set; }
        public string Dob { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    // Модель для окна "Лица в погранзоне"
    public class PersonInZone
    {
        public string FullName { get; set; } = string.Empty;
        public string Dob { get; set; } = string.Empty;
        public string PassportData { get; set; } = string.Empty;
        public string Citizenship { get; set; } = string.Empty;
        public string DestinationTown { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string VehicleInfo { get; set; } = string.Empty;
    }
}
