using Microsoft.Data.Sqlite;
using Dapper;
using System.IO;
using CheckpointApp.Models;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CheckpointApp.DataAccess
{
    public class DatabaseService
    {
        private readonly string _databasePath;

        public DatabaseService()
        {
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
                    dob TEXT,
                    info TEXT,
                    actions TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );",
                @"CREATE TABLE IF NOT EXISTS watchlist_persons (
                    id INTEGER PRIMARY KEY,
                    last_name TEXT NOT NULL,
                    first_name TEXT NOT NULL,
                    patronymic TEXT,
                    dob TEXT,
                    reason TEXT
                );"
            };

            foreach (var command in tableCommands)
            {
                connection.Execute(command);
            }
        }

        #region User Methods
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    id AS ID,
                    username AS Username,
                    password_hash AS PasswordHash,
                    is_admin AS IsAdmin,
                    created_at AS CreatedAt
                FROM users
                WHERE username = @Username";
            return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Username = username.ToUpper() });
        }

        public async Task<bool> AddUserAsync(User user)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO users (username, password_hash, is_admin)
                  VALUES ($username, $password_hash, $is_admin)";

            command.Parameters.AddWithValue("$username", user.Username);
            command.Parameters.AddWithValue("$password_hash", user.PasswordHash);
            command.Parameters.AddWithValue("$is_admin", user.IsAdmin);

            var affectedRows = await command.ExecuteNonQueryAsync();

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
            var sql = @"
                SELECT
                    id AS ID,
                    username AS Username,
                    password_hash AS PasswordHash,
                    is_admin AS IsAdmin,
                    created_at AS CreatedAt
                FROM users
                ORDER BY username";
            return await connection.QueryAsync<User>(sql);
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM users WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            return affectedRows > 0;
        }
        #endregion

        #region Crossing Methods
        public async Task<IEnumerable<Crossing>> GetAllCrossingsAsync()
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    c.id AS ID,
                    c.person_id AS PersonId,
                    c.vehicle_id AS VehicleId,
                    c.direction AS Direction,
                    c.purpose AS Purpose,
                    c.destination_town AS DestinationTown,
                    c.crossing_type AS CrossingType,
                    c.operator_id AS OperatorId,
                    c.timestamp AS Timestamp,
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

        public async Task<int> CreateCrossingAsync(Crossing crossing)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO crossings (person_id, vehicle_id, direction, purpose, destination_town, crossing_type, operator_id, timestamp)
                VALUES (@PersonId, @VehicleId, @Direction, @Purpose, @DestinationTown, @CrossingType, @OperatorId, @Timestamp)
                RETURNING id;";

            var parameters = new DynamicParameters();
            parameters.Add("PersonId", crossing.PersonId);
            parameters.Add("VehicleId", crossing.VehicleId);
            parameters.Add("Direction", crossing.Direction);
            parameters.Add("Purpose", crossing.Purpose);
            parameters.Add("DestinationTown", crossing.DestinationTown);
            parameters.Add("CrossingType", crossing.CrossingType);
            parameters.Add("OperatorId", crossing.OperatorId);
            parameters.Add("Timestamp", crossing.Timestamp);

            return await connection.ExecuteScalarAsync<int>(sql, parameters);
        }

        public async Task<IEnumerable<Crossing>> GetCrossingsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    c.id AS ID,
                    c.person_id AS PersonId,
                    c.vehicle_id AS VehicleId,
                    c.direction AS Direction,
                    c.purpose AS Purpose,
                    c.destination_town AS DestinationTown,
                    c.crossing_type AS CrossingType,
                    c.operator_id AS OperatorId,
                    c.timestamp AS Timestamp,
                    p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.patronymic, '') AS FullName,
                    p.dob as PersonDob,
                    p.passport_data as PersonPassport,
                    p.citizenship AS Citizenship,
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
                EndDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        public async Task<IEnumerable<string>> GetDistinctValuesAsync(string tableName, string columnName)
        {
            using var connection = GetConnection();
            if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[a-zA-Z0-9_]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(columnName, @"^[a-zA-Z0-9_]+$"))
            {
                throw new ArgumentException("Invalid table or column name.");
            }
            var sql = $"SELECT DISTINCT {columnName} FROM {tableName} WHERE {columnName} IS NOT NULL AND {columnName} != '' ORDER BY {columnName}";
            return await connection.QueryAsync<string>(sql);
        }
        #endregion

        #region Person/Vehicle Methods
        public async Task<Person?> FindPersonByPassportAsync(string passportData)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    id AS Id,
                    last_name AS LastName,
                    first_name AS FirstName,
                    patronymic AS Patronymic,
                    dob AS Dob,
                    citizenship AS Citizenship,
                    passport_data AS PassportData,
                    notes AS Notes
                FROM persons
                WHERE passport_data = @PassportData";
            return await connection.QuerySingleOrDefaultAsync<Person>(sql, new { PassportData = passportData.ToUpper() });
        }

        public async Task<Vehicle?> FindVehicleByLicensePlateAsync(string licensePlate)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    id AS ID,
                    make AS Make,
                    license_plate AS LicensePlate
                FROM vehicles
                WHERE license_plate = @LicensePlate";
            return await connection.QuerySingleOrDefaultAsync<Vehicle>(sql, new { LicensePlate = licensePlate.ToUpper() });
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
        #endregion

        #region Goods Methods
        public async Task AddGoodsAsync(IEnumerable<Good> goods)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO goods (crossing_id, description, quantity, unit)
                VALUES (@CrossingId, @Description, @Quantity, @Unit);";
            await connection.ExecuteAsync(sql, goods);
        }
        #endregion

        #region Security Lists Methods
        public async Task<IEnumerable<WantedPerson>> GetWantedPersonsAsync()
        {
            // --- ИСПРАВЛЕНИЕ: Явное сопоставление полей ---
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    id AS ID,
                    last_name AS LastName,
                    first_name AS FirstName,
                    patronymic AS Patronymic,
                    dob AS Dob,
                    info AS Info,
                    actions AS Actions,
                    created_at AS CreatedAt
                FROM wanted_persons";
            return await connection.QueryAsync<WantedPerson>(sql);
        }

        public async Task<int> AddWantedPersonAsync(WantedPerson person)
        {
            // --- ИСПРАВЛЕНИЕ: Явная передача параметров ---
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO wanted_persons (last_name, first_name, patronymic, dob, info, actions)
                VALUES (@LastName, @FirstName, @Patronymic, @Dob, @Info, @Actions)
                RETURNING id;";

            var parameters = new DynamicParameters();
            parameters.Add("LastName", person.LastName);
            parameters.Add("FirstName", person.FirstName);
            parameters.Add("Patronymic", person.Patronymic);
            parameters.Add("Dob", person.Dob);
            parameters.Add("Info", person.Info);
            parameters.Add("Actions", person.Actions);

            return await connection.ExecuteScalarAsync<int>(sql, parameters);
        }

        public async Task<bool> DeleteWantedPersonAsync(int id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM wanted_persons WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            return affectedRows > 0;
        }

        public async Task<IEnumerable<WatchlistPerson>> GetWatchlistPersonsAsync()
        {
            // --- ИСПРАВЛЕНИЕ: Явное сопоставление полей ---
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    id AS ID,
                    last_name AS LastName,
                    first_name AS FirstName,
                    patronymic AS Patronymic,
                    dob AS Dob,
                    reason AS Reason
                FROM watchlist_persons";
            return await connection.QueryAsync<WatchlistPerson>(sql);
        }

        public async Task<int> AddWatchlistPersonAsync(WatchlistPerson person)
        {
            // --- ИСПРАВЛЕНИЕ: Явная передача параметров ---
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO watchlist_persons (last_name, first_name, patronymic, dob, reason)
                VALUES (@LastName, @FirstName, @Patronymic, @Dob, @Reason)
                RETURNING id;";

            var parameters = new DynamicParameters();
            parameters.Add("LastName", person.LastName);
            parameters.Add("FirstName", person.FirstName);
            parameters.Add("Patronymic", person.Patronymic);
            parameters.Add("Dob", person.Dob);
            parameters.Add("Reason", person.Reason);

            return await connection.ExecuteScalarAsync<int>(sql, parameters);
        }

        public async Task<bool> DeleteWatchlistPersonAsync(int id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM watchlist_persons WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
            return affectedRows > 0;
        }

        public async Task<int> GetWantedPersonsCountAsync()
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM wanted_persons");
        }

        public async Task<int> GetWatchlistPersonsCountAsync()
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM watchlist_persons");
        }
        #endregion

        #region Dashboard and Analytics Methods

        public async Task<IEnumerable<PersonInZone>> GetPersonsInZoneAsync()
        {
            using var connection = GetConnection();
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

        public async Task<DashboardStats> GetDashboardStatsAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    SUM(CASE WHEN direction = 'ВЪЕЗД' THEN 1 ELSE 0 END) AS EnteredPersons,
                    COUNT(DISTINCT CASE WHEN direction = 'ВЪЕЗД' THEN vehicle_id END) AS EnteredVehicles,
                    SUM(CASE WHEN direction = 'ВЫЕЗД' THEN 1 ELSE 0 END) AS ExitedPersons,
                    COUNT(DISTINCT CASE WHEN direction = 'ВЫЕЗД' THEN vehicle_id END) AS ExitedVehicles
                FROM crossings
                WHERE timestamp BETWEEN @StartDate AND @EndDate";

            var result = await connection.QuerySingleOrDefaultAsync<DashboardStats>(sql, new
            {
                StartDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
            });
            return result ?? new DashboardStats();
        }
        #endregion
    }
}
