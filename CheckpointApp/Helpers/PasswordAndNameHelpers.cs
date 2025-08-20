using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CheckpointApp.Helpers
{
    // Класс для работы с паролями
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            string hashOfInput = HashPassword(password);
            return StringComparer.OrdinalIgnoreCase.Compare(hashOfInput, hashedPassword) == 0;
        }
    }

    // Класс для нормализации имен согласно спецификации
    public static class NameNormalizer
    {
        // Паттерн для поиска повторяющихся кириллических букв
        private static readonly Regex _consecutiveLettersRegex = new Regex(@"([а-яА-Я])\1+", RegexOptions.Compiled);

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }
            // Приводим к верхнему регистру и заменяем повторяющиеся буквы на одну
            return _consecutiveLettersRegex.Replace(name.ToUpper(), "$1");
        }
    }
}
