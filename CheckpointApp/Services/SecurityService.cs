using System;
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
        public bool IsAllowed { get; set; } = true;
        public string Message { get; set; } = string.Empty;
        public bool IsWarning { get; set; }
        public bool IsOnWantedList { get; set; } = false;
        public bool IsOnWatchlist { get; set; } = false;
    }

    public class SecurityService
    {
        private readonly DatabaseService _databaseService;

        public SecurityService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<SecurityCheckResult> PerformChecksAsync(Person personToCheck, bool isProactive = false)
        {
            var wantedCheckResult = await CheckWantedListAsync(personToCheck);
            if (!wantedCheckResult.IsAllowed)
            {
                if (isProactive)
                {
                    return new SecurityCheckResult { IsAllowed = false, IsOnWantedList = true };
                }

                var continueSave = MessageBox.Show(wantedCheckResult.Message, "ЛИЦО В РОЗЫСКЕ / СОВПАДЕНИЯ",
                                                   MessageBoxButton.YesNo, MessageBoxImage.Error);
                return new SecurityCheckResult { IsAllowed = continueSave == MessageBoxResult.Yes, IsWarning = false, IsOnWantedList = true };
            }

            var watchlistCheckResult = await CheckWatchlistAsync(personToCheck);
            if (!watchlistCheckResult.IsAllowed)
            {
                if (isProactive)
                {
                    return new SecurityCheckResult { IsAllowed = false, IsOnWatchlist = true };
                }

                var continueSave = MessageBox.Show(watchlistCheckResult.Message, "Лицо в списке наблюдения",
                                                   MessageBoxButton.YesNo, MessageBoxImage.Warning);
                System.Diagnostics.Debug.WriteLine($"WARNING: Operator confirmed saving for a person on the watchlist: {personToCheck.LastName}");
                return new SecurityCheckResult { IsAllowed = continueSave == MessageBoxResult.Yes, IsWarning = true, IsOnWatchlist = true };
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        // --- НОВЫЙ ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ НАДЕЖНОГО СРАВНЕНИЯ ДАТ ---
        private bool AreDatesMatching(string dateStr1, string dateStr2)
        {
            if (string.IsNullOrWhiteSpace(dateStr1) || string.IsNullOrWhiteSpace(dateStr2))
            {
                return false; // Если одна из дат пустая, они не совпадают
            }

            // Пытаемся преобразовать строки в даты
            bool success1 = DateTime.TryParse(dateStr1, out var date1);
            bool success2 = DateTime.TryParse(dateStr2, out var date2);

            if (success1 && success2)
            {
                // Если обе даты успешно преобразованы, сравниваем их без учета времени
                return date1.Date == date2.Date;
            }

            // Если преобразовать не удалось, возвращаемся к простому сравнению строк (на всякий случай)
            return string.Equals(dateStr1.Trim(), dateStr2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<SecurityCheckResult> CheckWantedListAsync(Person personToCheck)
        {
            var wantedPersons = await _databaseService.GetWantedPersonsAsync();
            var matches = new List<string>();

            var normalizedLastName = NameNormalizer.NormalizeName(personToCheck.LastName);
            var normalizedFirstName = NameNormalizer.NormalizeName(personToCheck.FirstName);
            var normalizedPatronymic = NameNormalizer.NormalizeName(personToCheck.Patronymic ?? "");

            var exactMatch = wantedPersons.FirstOrDefault(wp =>
                wp.LastName.Equals(personToCheck.LastName, StringComparison.OrdinalIgnoreCase) &&
                wp.FirstName.Equals(personToCheck.FirstName, StringComparison.OrdinalIgnoreCase) &&
                (wp.Patronymic ?? "").Equals((personToCheck.Patronymic ?? ""), StringComparison.OrdinalIgnoreCase) &&
                AreDatesMatching(wp.Dob, personToCheck.Dob)); // <-- ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД СРАВНЕНИЯ

            if (exactMatch != null)
            {
                matches.Add(BuildMatchMessage("!!! ПОЛНОЕ СОВПАДЕНИЕ ДАННЫХ !!!", exactMatch));
            }
            else
            {
                foreach (var wp in wantedPersons)
                {
                    var dbNormLastName = NameNormalizer.NormalizeName(wp.LastName);
                    var dbNormFirstName = NameNormalizer.NormalizeName(wp.FirstName);
                    var dbNormPatronymic = NameNormalizer.NormalizeName(wp.Patronymic ?? "");

                    if (dbNormLastName == normalizedLastName && dbNormFirstName == normalizedFirstName)
                        matches.Add(BuildMatchMessage("Совпадение Фамилии и Имени", wp));
                    else if (dbNormLastName == normalizedLastName && dbNormFirstName == normalizedFirstName && dbNormPatronymic == normalizedPatronymic)
                        matches.Add(BuildMatchMessage("Совпадение ФИО", wp));
                    else if (dbNormFirstName == normalizedFirstName && dbNormPatronymic == normalizedPatronymic && !string.IsNullOrEmpty(normalizedPatronymic))
                        matches.Add(BuildMatchMessage("Совпадение Имени и Отчества", wp));
                    else if (dbNormLastName == normalizedLastName && AreDatesMatching(wp.Dob, personToCheck.Dob)) // <-- ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД СРАВНЕНИЯ
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
                p.LastName.Equals(personToCheck.LastName, StringComparison.OrdinalIgnoreCase) &&
                p.FirstName.Equals(personToCheck.FirstName, StringComparison.OrdinalIgnoreCase) &&
                AreDatesMatching(p.Dob, personToCheck.Dob)); // <-- ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД СРАВНЕНИЯ

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
