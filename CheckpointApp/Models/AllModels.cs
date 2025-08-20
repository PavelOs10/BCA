// Рекомендуется разместить каждый класс в своем файле,
// но для удобства просмотра я объединю их здесь.

namespace CheckpointApp.Models
{
    // Модель пользователя системы
    public class User
    {
        public int ID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Модель физического лица
    public class Person
    {
        public int ID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string? Patronymic { get; set; }
        public string Dob { get; set; } // Дата рождения в формате "ДД.ММ.ГГГГ"
        public string Citizenship { get; set; }
        public string PassportData { get; set; }
        public string? Notes { get; set; }
    }

    // Модель транспортного средства
    public class Vehicle
    {
        public int ID { get; set; }
        public string Make { get; set; }
        public string LicensePlate { get; set; }
    }

    // Модель события пересечения границы
    public class Crossing
    {
        public int ID { get; set; }
        public int PersonId { get; set; }
        public int? VehicleId { get; set; }
        public string Direction { get; set; } // "ВЪЕЗД" или "ВЫЕЗД"
        public string? Purpose { get; set; }
        public string? DestinationTown { get; set; }
        public string CrossingType { get; set; } // "ПЕШКОМ", "ВОДИТЕЛЬ", "ПАССАЖИР"
        public int OperatorId { get; set; }
        public string Timestamp { get; set; } // "ГГГГ-ММ-ДД ЧЧ:ММ:СС"

        // Свойства для отображения в журнале
        public string FullName { get; set; }
        public string PersonDob { get; set; }
        public string PersonPassport { get; set; }
        public string VehicleInfo { get; set; }
        public string OperatorUsername { get; set; }
    }

    // Модель товара/груза
    public class Good
    {
        public int ID { get; set; }
        public int CrossingId { get; set; }
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }

    // Модель для временного хранения товаров перед сохранением пересечения
    public class TempGood
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }

    // Модель лица в списке розыска
    public class WantedPerson
    {
        public int ID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string? Patronymic { get; set; }
        public string Dob { get; set; }
        public string Info { get; set; }
        public string Actions { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Модель лица в списке наблюдения
    public class WatchlistPerson
    {
        public int ID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string? Patronymic { get; set; }
        public string Dob { get; set; }
        public string Reason { get; set; }
    }
}
