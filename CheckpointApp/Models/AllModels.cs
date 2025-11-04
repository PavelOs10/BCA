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

    // Модель физического лица
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

        public string FullName => $"{LastName} {FirstName} {Patronymic}".Trim();
    }

    // Модель транспортного средства
    public partial class Vehicle : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _make = string.Empty;
        [ObservableProperty] private string _licensePlate = string.Empty;
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
        public int? DriverCrossingId { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string PersonDob { get; set; } = string.Empty;
        public string PersonPassport { get; set; } = string.Empty;
        public string VehicleInfo { get; set; } = string.Empty;
        public string OperatorUsername { get; set; } = string.Empty;
        public string? Citizenship { get; set; }
        public bool IsOnWantedList { get; set; }
        public bool IsOnWatchlist { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class Good
    {
        public int ID { get; set; }
        public int CrossingId { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class TempGood
    {
        public string Description { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

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

    public class WatchlistPerson
    {
        public int ID { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? Patronymic { get; set; }
        public string Dob { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

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

    public class VehicleInZone
    {
        public string VehicleInfo { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string LastDriverFullName { get; set; } = string.Empty;
    }

    public class DashboardStats
    {
        public int EnteredPersons { get; set; }
        public int EnteredVehicles { get; set; }
        public int ExitedPersons { get; set; }
        public int ExitedVehicles { get; set; }
    }

    public class GoodReportItem
    {
        public string Description { get; set; } = string.Empty;
        public double TotalQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class PersonGoodsItem
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class InZoneStats
    {
        public int PersonCount { get; set; }
        public int VehicleCount { get; set; }
    }

    // --- НОВАЯ МОДЕЛЬ: Для хранения информации о попутчиках ---
    public class TravelCompanion
    {
        public string Timestamp { get; set; } = string.Empty;
        public string VehicleInfo { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Dob { get; set; } = string.Empty;
    }
}

