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

            // --- ИЗМЕНЕНИЕ 3: Добавлено поле driver_crossing_id для связи пассажиров с водителем ---
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
                    is_deleted BOOLEAN NOT NULL DEFAULT 0,
                    driver_crossing_id INTEGER,
                    FOREIGN KEY (person_id) REFERENCES persons(id) ON DELETE CASCADE,
                    FOREIGN KEY (vehicle_id) REFERENCES vehicles(id) ON DELETE SET NULL,
                    FOREIGN KEY (operator_id) REFERENCES users(id),
                    FOREIGN KEY (driver_crossing_id) REFERENCES crossings(id) ON DELETE SET NULL
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

            var crossingsTableInfo = connection.Query<dynamic>("PRAGMA table_info(crossings);").ToList();
            if (!crossingsTableInfo.Any(col => col.name.ToString().Equals("is_deleted", StringComparison.OrdinalIgnoreCase)))
            {
                connection.Execute("ALTER TABLE crossings ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT 0;");
            }
            if (!crossingsTableInfo.Any(col => col.name.ToString().Equals("driver_crossing_id", StringComparison.OrdinalIgnoreCase)))
            {
                connection.Execute("ALTER TABLE crossings ADD COLUMN driver_crossing_id INTEGER REFERENCES crossings(id) ON DELETE SET NULL;");
            }
            // --- КОНЕЦ ИЗМЕНЕНИЯ 3 ---
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
        // --- ИЗМЕНЕНИЕ 3: Добавлено поле driver_crossing_id в выборку ---
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
                    c.is_deleted as IsDeleted,
                    c.driver_crossing_id as DriverCrossingId,
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
                ORDER BY c.timestamp DESC";
            return await connection.QueryAsync<Crossing>(sql);
        }

        public async Task<Crossing?> GetCrossingByIdAsync(int crossingId)
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
                    c.is_deleted as IsDeleted,
                    c.driver_crossing_id as DriverCrossingId,
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
                WHERE c.id = @CrossingId";
            return await connection.QuerySingleOrDefaultAsync<Crossing>(sql, new { CrossingId = crossingId });
        }
        // --- КОНЕЦ ИЗМЕНЕНИЯ 3 ---

        public async Task<IEnumerable<Crossing>> GetAllCrossingsByPersonIdAsync(int personId)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    c.timestamp AS Timestamp,
                    c.direction AS Direction,
                    c.crossing_type AS CrossingType,
                    c.purpose AS Purpose,
                    c.destination_town AS DestinationTown
                FROM crossings c
                WHERE c.is_deleted = 0 AND c.person_id = @PersonId
                ORDER BY c.timestamp DESC";
            return await connection.QueryAsync<Crossing>(sql, new { PersonId = personId });
        }

        // --- ИЗМЕНЕНИЕ 3: Добавлено поле driver_crossing_id в запрос ---
        public async Task<int> CreateCrossingAsync(Crossing crossing)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO crossings (person_id, vehicle_id, direction, purpose, destination_town, crossing_type, operator_id, timestamp, driver_crossing_id)
                VALUES (@PersonId, @VehicleId, @Direction, @Purpose, @DestinationTown, @CrossingType, @OperatorId, @Timestamp, @DriverCrossingId)
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
            parameters.Add("DriverCrossingId", crossing.DriverCrossingId);

            return await connection.ExecuteScalarAsync<int>(sql, parameters);
        }

        public async Task<bool> MarkCrossingAsDeletedAsync(int crossingId)
        {
            using var connection = GetConnection();
            var sql = "UPDATE crossings SET is_deleted = 1 WHERE id = @CrossingId";
            var affectedRows = await connection.ExecuteAsync(sql, new { CrossingId = crossingId });
            return affectedRows > 0;
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
                WHERE c.is_deleted = 0 AND c.timestamp BETWEEN @StartDate AND @EndDate
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

        public async Task<Crossing?> GetLastCrossingByPersonIdAsync(int personId)
        {
            using var connection = GetConnection();
            var sql = "SELECT direction AS Direction FROM crossings WHERE is_deleted = 0 AND person_id = @PersonId ORDER BY timestamp DESC LIMIT 1";
            return await connection.QuerySingleOrDefaultAsync<Crossing>(sql, new { PersonId = personId });
        }

        public async Task<int?> GetPreviousDriverCrossingIdAsync(int driverPersonId, int currentCrossingId)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT id FROM crossings
                WHERE person_id = @DriverPersonId
                  AND crossing_type = 'ВОДИТЕЛЬ'
                  AND id != @CurrentCrossingId
                ORDER BY timestamp DESC
                LIMIT 1;";
            return await connection.QuerySingleOrDefaultAsync<int?>(sql, new { DriverPersonId = driverPersonId, CurrentCrossingId = currentCrossingId });
        }

        public async Task<IEnumerable<Crossing>> GetPassengerCrossingsByDriverCrossingIdAsync(int driverCrossingId)
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
                    c.is_deleted as IsDeleted,
                    c.driver_crossing_id as DriverCrossingId,
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
                WHERE c.driver_crossing_id = @DriverCrossingId
                  AND c.crossing_type = 'ПАССАЖИР'
                ORDER BY p.last_name, p.first_name;";
            return await connection.QueryAsync<Crossing>(sql, new { DriverCrossingId = driverCrossingId });
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

        public async Task<Person?> GetPersonByIdAsync(int personId)
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
                WHERE id = @PersonId";
            return await connection.QuerySingleOrDefaultAsync<Person>(sql, new { PersonId = personId });
        }

        public async Task<IEnumerable<Person>> GetAllPersonsAsync()
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
                ORDER BY last_name, first_name";
            return await connection.QueryAsync<Person>(sql);
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

        public async Task<IEnumerable<Vehicle>> GetVehiclesByPersonIdAsync(int personId)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT DISTINCT
                    v.make AS Make,
                    v.license_plate AS LicensePlate
                FROM vehicles v
                JOIN crossings c ON v.id = c.vehicle_id
                WHERE c.is_deleted = 0 AND c.person_id = @PersonId";
            return await connection.QueryAsync<Vehicle>(sql, new { PersonId = personId });
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


        public async Task<bool> UpdatePersonAsync(Person person)
        {
            using var connection = GetConnection();
            var sql = @"
                UPDATE persons SET
                    last_name = @LastName,
                    first_name = @FirstName,
                    patronymic = @Patronymic,
                    dob = @Dob,
                    citizenship = @Citizenship,
                    passport_data = @PassportData,
                    notes = @Notes
                WHERE id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, person);
            return affectedRows > 0;
        }

        public async Task<bool> UpdatePersonNotesAsync(int personId, string? notes)
        {
            using var connection = GetConnection();
            var sql = "UPDATE persons SET notes = @Notes WHERE id = @PersonId";
            var affectedRows = await connection.ExecuteAsync(sql, new { Notes = notes, PersonId = personId });
            return affectedRows > 0;
        }
        public async Task<IEnumerable<TravelCompanion>> GetTravelCompanionsAsync(int personId)
        {
            using var connection = GetConnection();
            var sql = @"
                -- 1. Находим все поездки целевого лица (personId), где он был водителем или пассажиром
                WITH PersonTrips AS (
                    SELECT 
                        c.id as TripId, 
                        c.driver_crossing_id as DriverTripId, 
                        c.timestamp as TripTimestamp,
                        c.crossing_type as PersonRole
                    FROM crossings c
                    WHERE c.person_id = @PersonId AND c.vehicle_id IS NOT NULL AND c.is_deleted = 0
                ),
                -- 2. Определяем ID поездки водителя для каждой поездки целевого лица
                DriverTripIds AS (
                    SELECT 
                        CASE 
                            WHEN pt.PersonRole = 'ВОДИТЕЛЬ' THEN pt.TripId
                            ELSE pt.DriverTripId
                        END as DriverCrossingId,
                        pt.TripTimestamp
                    FROM PersonTrips pt
                )
                -- 3. Находим все пересечения (и водителей, и пассажиров), связанные с этими поездками,
                --    ИСКЛЮЧАЯ само целевое лицо
                SELECT
                    c.timestamp AS Timestamp,
                    IFNULL(v.make || '/' || v.license_plate, '') AS VehicleInfo,
                    c.crossing_type AS Role,
                    p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.patronymic, '') AS FullName,
                    p.dob AS Dob
                FROM crossings c
                JOIN persons p ON c.person_id = p.id
                LEFT JOIN vehicles v ON c.vehicle_id = v.id
                WHERE 
                    (
                        c.driver_crossing_id IN (SELECT DriverCrossingId FROM DriverTripIds) OR -- все пассажиры этой поездки
                        c.id IN (SELECT DriverCrossingId FROM DriverTripIds) -- водитель этой поездки
                    )
                    AND c.person_id != @PersonId -- исключаем самого человека
                    AND c.is_deleted = 0
                ORDER BY c.timestamp DESC;
            ";

            return await connection.QueryAsync<TravelCompanion>(sql, new { PersonId = personId });
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

        public async Task<IEnumerable<GoodReportItem>> GetGoodsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    g.description AS Description,
                    SUM(g.quantity) AS TotalQuantity,
                    g.unit AS Unit
                FROM goods g
                JOIN crossings c ON g.crossing_id = c.id
                WHERE c.is_deleted = 0 AND c.timestamp BETWEEN @StartDate AND @EndDate
                GROUP BY g.description, g.unit
                ORDER BY TotalQuantity DESC;
            ";
            return await connection.QueryAsync<GoodReportItem>(sql, new
            {
                StartDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        public async Task<IEnumerable<PersonGoodsItem>> GetGoodsByPersonIdAndDateRangeAsync(int personId, DateTime startDate, DateTime endDate)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    c.timestamp AS Timestamp,
                    c.direction AS Direction,
                    g.description AS Description,
                    g.quantity AS Quantity,
                    g.unit AS Unit
                FROM goods g
                JOIN crossings c ON g.crossing_id = c.id
                WHERE c.is_deleted = 0 AND c.person_id = @PersonId
                  AND c.timestamp BETWEEN @StartDate AND @EndDate
                ORDER BY c.timestamp DESC;
            ";
            return await connection.QueryAsync<PersonGoodsItem>(sql, new
            {
                PersonId = personId,
                StartDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        public async Task<IEnumerable<GoodReportItem>> GetGoodsSummaryByPersonIdAsync(int personId)
        {
            using var connection = GetConnection();
            var sql = @"
                SELECT
                    g.description AS Description,
                    SUM(g.quantity) AS TotalQuantity,
                    g.unit AS Unit
                FROM goods g
                JOIN crossings c ON g.crossing_id = c.id
                WHERE c.is_deleted = 0 AND c.person_id = @PersonId
                GROUP BY g.description, g.unit
                ORDER BY TotalQuantity DESC;
            ";
            return await connection.QueryAsync<GoodReportItem>(sql, new { PersonId = personId });
        }
        #endregion

        #region Security Lists Methods
        public async Task<IEnumerable<WantedPerson>> GetWantedPersonsAsync()
        {
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
                WITH LastPersonCrossing AS (
                    SELECT
                        person_id,
                        MAX(timestamp) AS last_timestamp
                    FROM crossings
                    WHERE is_deleted = 0
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
                JOIN LastPersonCrossing lpc ON c.person_id = lpc.person_id AND c.timestamp = lpc.last_timestamp
                JOIN persons p ON c.person_id = p.id
                LEFT JOIN vehicles v ON c.vehicle_id = v.id
                WHERE c.direction = 'ВЪЕЗД'
                ORDER BY p.last_name, p.first_name;
            ";
            return await connection.QueryAsync<PersonInZone>(sql);
        }

        public async Task<IEnumerable<VehicleInZone>> GetVehiclesInZoneAsync()
        {
            using var connection = GetConnection();
            var sql = @"
                WITH LastVehicleCrossing AS (
                    SELECT
                        vehicle_id,
                        MAX(timestamp) AS last_timestamp
                    FROM crossings
                    WHERE is_deleted = 0 AND vehicle_id IS NOT NULL
                    GROUP BY vehicle_id
                )
                SELECT
                    v.make || '/' || v.license_plate AS VehicleInfo,
                    c.timestamp AS Timestamp,
                    p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.patronymic, '') AS LastDriverFullName
                FROM crossings c
                JOIN LastVehicleCrossing lvc ON c.vehicle_id = lvc.vehicle_id AND c.timestamp = lvc.last_timestamp
                JOIN vehicles v ON c.vehicle_id = v.id
                JOIN persons p ON c.person_id = p.id
                WHERE c.direction = 'ВЪЕЗД'
                ORDER BY v.make, v.license_plate;
            ";
            return await connection.QueryAsync<VehicleInZone>(sql);
        }

        public async Task<InZoneStats> GetInZoneStatsAsync()
        {
            using var connection = GetConnection();
            var personSql = @"
                WITH LastPersonCrossing AS (
                    SELECT
                        person_id,
                        MAX(timestamp) AS last_timestamp
                    FROM crossings
                    WHERE is_deleted = 0
                    GROUP BY person_id
                )
                SELECT COUNT(c.person_id)
                FROM crossings c
                JOIN LastPersonCrossing lpc ON c.person_id = lpc.person_id AND c.timestamp = lpc.last_timestamp
                WHERE c.direction = 'ВЪЕЗД';
            ";

            var vehicleSql = @"
                WITH LastVehicleCrossing AS (
                    SELECT
                        vehicle_id,
                        MAX(timestamp) AS last_timestamp
                    FROM crossings
                    WHERE is_deleted = 0 AND vehicle_id IS NOT NULL
                    GROUP BY vehicle_id
                )
                SELECT COUNT(c.vehicle_id)
                FROM crossings c
                JOIN LastVehicleCrossing lvc ON c.vehicle_id = lvc.vehicle_id AND c.timestamp = lvc.last_timestamp
                WHERE c.direction = 'ВЪЕЗД';
            ";

            var personCount = await connection.ExecuteScalarAsync<int>(personSql);
            var vehicleCount = await connection.ExecuteScalarAsync<int>(vehicleSql);

            return new InZoneStats { PersonCount = personCount, VehicleCount = vehicleCount };
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
                WHERE is_deleted = 0 AND timestamp BETWEEN @StartDate AND @EndDate";

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




