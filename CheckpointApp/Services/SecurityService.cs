using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CheckpointApp.DataAccess;
using CheckpointApp.Helpers;
using CheckpointApp.Models;

namespace CheckpointApp.Services
{
    public class SecurityCheckResult
    {
        public bool IsAllowed { get; set; }
        public string Message { get; set; }
        public bool IsWarning { get; set; } // True for watchlist, false for wanted list
    }

    public class SecurityService
    {
        private readonly DatabaseService _databaseService;

        public SecurityService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<SecurityCheckResult> PerformChecksAsync(Person personToCheck)
        {
            // 1. Проверка по списку "Розыск"
            var wantedCheckResult = await CheckWantedListAsync(personToCheck);
            if (!wantedCheckResult.IsAllowed)
            {
                // Если найдено совпадение в списке розыска, показываем диалог и возвращаем результат
                var continueSave = MessageBox.Show(wantedCheckResult.Message, "ЛИЦО В РОЗЫСКЕ / СОВПАДЕНИЯ",
                                                   MessageBoxButton.YesNo, MessageBoxImage.Error);

                return new SecurityCheckResult { IsAllowed = continueSave == MessageBoxResult.Yes, IsWarning = false };
            }

            // 2. Проверка по "Списку наблюдения"
            var watchlistCheckResult = await CheckWatchlistAsync(personToCheck);
            if (!watchlistCheckResult.IsAllowed)
            {
                var continueSave = MessageBox.Show(watchlistCheckResult.Message, "Лицо в списке наблюдения",
                                                   MessageBoxButton.YesNo, MessageBoxImage.Warning);

                // Логируем действие с уровнем "warning" (здесь можно добавить логирование в файл или БД)
                System.Diagnostics.Debug.WriteLine($"WARNING: Operator confirmed saving for a person on the watchlist: {personToCheck.LastName}");

                return new SecurityCheckResult { IsAllowed = continueSave == MessageBoxResult.Yes, IsWarning = true };
            }

            // Если проверок не пройдено
            return new SecurityCheckResult { IsAllowed = true };
        }

        private async Task<SecurityCheckResult> CheckWantedListAsync(Person personToCheck)
        {
            var wantedPersons = await _databaseService.GetWantedPersonsAsync();
            var matches = new List<string>();

            var normalizedLastName = NameNormalizer.NormalizeName(personToCheck.LastName);
            var normalizedFirstName = NameNormalizer.NormalizeName(personToCheck.FirstName);
            var normalizedPatronymic = NameNormalizer.NormalizeName(personToCheck.Patronymic ?? "");

            // --- Точное совпадение ---
            var exactMatch = wantedPersons.FirstOrDefault(wp =>
                wp.LastName.ToUpper() == personToCheck.LastName.ToUpper() &&
                wp.FirstName.ToUpper() == personToCheck.FirstName.ToUpper() &&
                (wp.Patronymic ?? "").ToUpper() == (personToCheck.Patronymic ?? "").ToUpper() &&
                wp.Dob == personToCheck.Dob);

            if (exactMatch != null)
            {
                matches.Add(BuildMatchMessage("!!! ПОЛНОЕ СОВПАДЕНИЕ ДАННЫХ !!!", exactMatch));
            }
            else // --- Частичное (нечеткое) совпадение ---
            {
                foreach (var wp in wantedPersons)
                {
                    var dbNormLastName = NameNormalizer.NormalizeName(wp.LastName);
                    var dbNormFirstName = NameNormalizer.NormalizeName(wp.FirstName);
                    var dbNormPatronymic = NameNormalizer.NormalizeName(wp.Patronymic ?? "");

                    // Критерий 1: Совпадение Фамилии и Имени
                    if (dbNormLastName == normalizedLastName && dbNormFirstName == normalizedFirstName)
                        matches.Add(BuildMatchMessage("Совпадение Фамилии и Имени", wp));
                    // Критерий 2: Совпадение ФИО
                    else if (dbNormLastName == normalizedLastName && dbNormFirstName == normalizedFirstName && dbNormPatronymic == normalizedPatronymic)
                        matches.Add(BuildMatchMessage("Совпадение ФИО", wp));
                    // Критерий 3: Совпадение Имени и Отчества
                    else if (dbNormFirstName == normalizedFirstName && dbNormPatronymic == normalizedPatronymic && !string.IsNullOrEmpty(normalizedPatronymic))
                        matches.Add(BuildMatchMessage("Совпадение Имени и Отчества", wp));
                    // Критерий 4: Совпадение Фамилии и Даты рождения
                    else if (dbNormLastName == normalizedLastName && wp.Dob == personToCheck.Dob)
                        matches.Add(BuildMatchMessage("Совпадение Фамилии и Даты рождения", wp));
                }
            }

            if (matches.Any())
            {
                var finalMessage = new StringBuilder();
                finalMessage.AppendLine("Обнаружены следующие совпадения в списке розыска:\n");
                finalMessage.AppendLine(string.Join("\n----------------------------------\n", matches.Distinct()));
                finalMessage.AppendLine("\nПродолжить сохранение пересечения?");
                return new SecurityCheckResult { IsAllowed = false, Message = finalMessage.ToString() };
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        private string BuildMatchMessage(string reason, WantedPerson wp)
        {
            return $"Причина: {reason}\n" +
                   $"ФИО в базе: {wp.LastName} {wp.FirstName} {wp.Patronymic}\n" +
                   $"Дата рождения: {wp.Dob}\n" +
                   $"Информация: {wp.Info}\n" +
                   $"Предписанные действия: {wp.Actions}";
        }

        private async Task<SecurityCheckResult> CheckWatchlistAsync(Person personToCheck)
        {
            var watchlist = await _databaseService.GetWatchlistPersonsAsync();
            var match = watchlist.FirstOrDefault(p =>
                p.LastName.ToUpper() == personToCheck.LastName.ToUpper() &&
                p.FirstName.ToUpper() == personToCheck.FirstName.ToUpper() &&
                p.Dob == personToCheck.Dob);

            if (match != null)
            {
                var message = $"Лицо найдено в списке наблюдения.\n\n" +
                              $"ФИО: {match.LastName} {match.FirstName} {match.Patronymic}\n" +
                              $"Дата рождения: {match.Dob}\n" +
                              $"Причина: {match.Reason}\n\n" +
                              "Продолжить сохранение?";
                return new SecurityCheckResult { IsAllowed = false, Message = message };
            }

            return new SecurityCheckResult { IsAllowed = true };
        }
    }
}
