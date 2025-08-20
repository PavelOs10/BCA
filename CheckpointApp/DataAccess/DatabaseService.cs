using Microsoft.Data.Sqlite;
using Dapper;
using System.IO;
using CheckpointApp.Models;

namespace CheckpointApp.DataAccess
{
    public class DatabaseService
    {
        private readonly string _databasePath;

        public DatabaseService()
        {
            // База данных будет создана в папке с приложением
            _databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "checkpoint_control.db");
            InitializeDatabase();
        }

        private SqliteConnection GetConnection()
        {
            return new SqliteConnection($"Data Source={_databasePath}");
        }

        public void InitializeDatabase()
        {
            using var connection = GetConnection();
            connection.Open();

            var tableCommands = new[]
            {
                @"CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY,
                    username TEXT NOT NULL UNIQUE,
                    password_hash TEXT NOT NULL,
                    is_admin BOOLEAN NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );",
                @"CREATE TABLE IF NOT EXISTS persons (
                    id INTEGER PRIMARY KEY,
                    last_name TEXT NOT NULL,
                    first_name TEXT NOT NULL,
                    patronymic TEXT,
                    dob TEXT NOT NULL,
                    citizenship TEXT NOT NULL,
                    passport_data TEXT NOT NULL UNIQUE,
                    notes TEXT
                );",
                @"CREATE TABLE IF NOT EXISTS vehicles (
                    id INTEGER PRIMARY KEY,
                    make TEXT NOT NULL,
                    license_plate TEXT NOT NULL UNIQUE
                );",
                @"CREATE TABLE IF NOT EXISTS crossings (
                    id INTEGER PRIMARY KEY,
                    person_id INTEGER NOT NULL,
                    vehicle_id INTEGER,
                    direction TEXT NOT NULL,
                    purpose TEXT,
                    destination_town TEXT,
                    crossing_type TEXT NOT NULL,
                    operator_id INTEGER NOT NULL,
                    timestamp TEXT NOT NULL,
                    FOREIGN KEY (person_id) REFERENCES persons(id) ON DELETE CASCADE,
                    FOREIGN KEY (vehicle_id) REFERENCES vehicles(id) ON DELETE SET NULL,
                    FOREIGN KEY (operator_id) REFERENCES users(id)
                );",
                @"CREATE TABLE IF NOT EXISTS goods (
                    id INTEGER PRIMARY KEY,
                    crossing_id INTEGER NOT NULL,
                    description TEXT NOT NULL,
                    quantity REAL,
                    unit TEXT,
                    FOREIGN KEY (crossing_id) REFERENCES crossings(id) ON DELETE CASCADE
                );",
                @"CREATE TABLE IF NOT EXISTS wanted_persons (
                    id INTEGER PRIMARY KEY,
                    last_name TEXT NOT NULL,
                    first_name TEXT NOT NULL,
                    patronymic TEXT,
                    dob TEXT NOT NULL,
                    info TEXT,
                    actions TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );",
                @"CREATE TABLE IF NOT EXISTS watchlist_persons (
                    id INTEGER PRIMARY KEY,
                    last_name TEXT NOT NULL,
                    first_name TEXT NOT NULL,
                    patronymic TEXT,
                    dob TEXT NOT NULL,
                    reason TEXT
                );"
            };

            foreach (var command in tableCommands)
            {
                connection.Execute(command);
            }
        }

        // --- Методы для работы с пользователями ---
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            using var connection = GetConnection();
            return await connection.QuerySingleOrDefaultAsync<User>(
                "SELECT * FROM users WHERE username = @Username", new { Username = username.ToUpper() });
        }

        public async Task<bool> AddUserAsync(User user)
        {
            using var connection = GetConnection();
            var sql = "INSERT INTO users (username, password_hash, is_admin) VALUES (@Username, @PasswordHash, @IsAdmin)";
            var affectedRows = await connection.ExecuteAsync(sql, new { user.Username, user.PasswordHash, user.IsAdmin });
            return affectedRows > 0;
        }

        public async Task<int> GetUserCountAsync()
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
        }
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            using var connection = GetConnection();
            return await connection.QueryAsync<User>("SELECT * FROM users ORDER BY username");
        }

        // --- НОВЫЙ МЕТОД: Удаление пользователя ---
        public async Task<bool> DeleteUserAsync(int id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM users WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            return affectedRows > 0;
        }
        public async Task<int> AddWantedPersonAsync(WantedPerson person)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO wanted_persons (last_name, first_name, patronymic, dob, info, actions)
                VALUES (@LastName, @FirstName, @Patronymic, @Dob, @Info, @Actions)
                RETURNING id;";
            return await connection.ExecuteScalarAsync<int>(sql, person);
        }

        // --- НОВЫЙ МЕТОД: Удаление лица из списка розыска ---
        public async Task<bool> DeleteWantedPersonAsync(int id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM wanted_persons WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            return affectedRows > 0;
        }
        public async Task<int> AddWatchlistPersonAsync(WatchlistPerson person)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO watchlist_persons (last_name, first_name, patronymic, dob, reason)
                VALUES (@LastName, @FirstName, @Patronymic, @Dob, @Reason)
                RETURNING id;";
            return await connection.ExecuteScalarAsync<int>(sql, person);
        }

        // --- НОВЫЙ МЕТОД: Удаление лица из списка наблюдения ---
        public async Task<bool> DeleteWatchlistPersonAsync(int id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM watchlist_persons WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            return affectedRows > 0;
        }
        public async Task<IEnumerable<Crossing>> GetAllCrossingsAsync()
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT 
                    c.*, 
                    p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.patronymic, '') AS FullName,
                    p.dob as PersonDob,
                    p.passport_data as PersonPassport,
                    IFNULL(v.make || '/' || v.license_plate, '') AS VehicleInfo,
                    u.username AS OperatorUsername
                FROM crossings c
                JOIN persons p ON c.person_id = p.id
                LEFT JOIN vehicles v ON c.vehicle_id = v.id
                JOIN users u ON c.operator_id = u.id
                ORDER BY c.timestamp DESC";
            return await connection.QueryAsync<Crossing>(sql);
        }

        public async Task<Person?> FindPersonByPassportAsync(string passportData)
        {
            using var connection = GetConnection();
            return await connection.QuerySingleOrDefaultAsync<Person>(
                "SELECT * FROM persons WHERE passport_data = @PassportData",
                new { PassportData = passportData.ToUpper() });
        }

        public async Task<Vehicle?> FindVehicleByLicensePlateAsync(string licensePlate)
        {
            using var connection = GetConnection();
            return await connection.QuerySingleOrDefaultAsync<Vehicle>(
                "SELECT * FROM vehicles WHERE license_plate = @LicensePlate",
                new { LicensePlate = licensePlate.ToUpper() });
        }

        public async Task<int> CreatePersonAsync(Person person)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO persons (last_name, first_name, patronymic, dob, citizenship, passport_data, notes)
                VALUES (@LastName, @FirstName, @Patronymic, @Dob, @Citizenship, @PassportData, @Notes)
                RETURNING id;";
            return await connection.ExecuteScalarAsync<int>(sql, person);
        }

        public async Task<int> CreateVehicleAsync(Vehicle vehicle)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO vehicles (make, license_plate)
                VALUES (@Make, @LicensePlate)
                RETURNING id;";
            return await connection.ExecuteScalarAsync<int>(sql, vehicle);
        }

        public async Task<int> CreateCrossingAsync(Crossing crossing)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO crossings (person_id, vehicle_id, direction, purpose, destination_town, crossing_type, operator_id, timestamp)
                VALUES (@PersonId, @VehicleId, @Direction, @Purpose, @DestinationTown, @CrossingType, @OperatorId, @Timestamp)
                RETURNING id;";
            return await connection.ExecuteScalarAsync<int>(sql, crossing);
        }

        public async Task AddGoodsAsync(IEnumerable<Good> goods)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO goods (crossing_id, description, quantity, unit)
                VALUES (@CrossingId, @Description, @Quantity, @Unit);";
            await connection.ExecuteAsync(sql, goods);
        }

        public async Task<IEnumerable<WantedPerson>> GetWantedPersonsAsync()
        {
            using var connection = GetConnection();
            return await connection.QueryAsync<WantedPerson>("SELECT * FROM wanted_persons");
        }

        public async Task<IEnumerable<WatchlistPerson>> GetWatchlistPersonsAsync()
        {
            using var connection = GetConnection();
            return await connection.QueryAsync<WatchlistPerson>("SELECT * FROM watchlist_persons");
        }
        public async Task<IEnumerable<PersonInZone>> GetPersonsInZoneAsync()
        {
            using var connection = GetConnection();
            // Этот запрос находит последнее пересечение для каждого человека
            // и возвращает данные, если это пересечение было "ВЪЕЗД".
            var sql = @"
                WITH LastCrossing AS (
                    SELECT
                        person_id,
                        MAX(timestamp) AS last_timestamp
                    FROM crossings
                    GROUP BY person_id
                )
                SELECT
                    p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.patronymic, '') AS FullName,
                    p.dob AS Dob,
                    p.passport_data AS PassportData,
                    p.citizenship AS Citizenship,
                    c.destination_town AS DestinationTown,
                    c.timestamp AS Timestamp,
                    IFNULL(v.make || '/' || v.license_plate, 'Пешком') AS VehicleInfo
                FROM crossings c
                JOIN LastCrossing lc ON c.person_id = lc.person_id AND c.timestamp = lc.last_timestamp
                JOIN persons p ON c.person_id = p.id
                LEFT JOIN vehicles v ON c.vehicle_id = v.id
                WHERE c.direction = 'ВЪЕЗД'
                ORDER BY c.destination_town, c.timestamp DESC;
            ";
            return await connection.QueryAsync<PersonInZone>(sql);
        }
        public async Task<IEnumerable<Crossing>> GetCrossingsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = GetConnection();
            // endDate.AddDays(1) используется для включения всего последнего дня в диапазон
            var sql = @"
                SELECT 
                    c.*, 
                    p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.patronymic, '') AS FullName,
                    p.dob as PersonDob,
                    p.passport_data as PersonPassport,
                    p.citizenship, -- Добавляем гражданство для аналитики
                    IFNULL(v.make || '/' || v.license_plate, '') AS VehicleInfo,
                    u.username AS OperatorUsername
                FROM crossings c
                JOIN persons p ON c.person_id = p.id
                LEFT JOIN vehicles v ON c.vehicle_id = v.id
                JOIN users u ON c.operator_id = u.id
                WHERE c.timestamp BETWEEN @StartDate AND @EndDate
                ORDER BY c.timestamp DESC";

            return await connection.QueryAsync<Crossing>(sql, new
            {
                StartDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = endDate.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }
}
