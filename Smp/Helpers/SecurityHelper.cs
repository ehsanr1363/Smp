using System;
using System.Security.Cryptography;
using System.Text;

namespace SMP.Helpers
{
    public static class SecurityHelper
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;

        public static string HashPassword(string password)
        {
            if (password == null) password = "";
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash)) return false;
            if (enteredPassword == null) enteredPassword = "";

            // فرمت جدید
            if (storedHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
            {
                var parts = storedHash.Split('$');
                if (parts.Length != 4) return false;
                if (!int.TryParse(parts[1], out int iterations)) return false;
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);

                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    enteredPassword, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            // فرمت قدیمی: SHA256 hex (64 کاراکتر)
            if (storedHash.Length == 64 && IsHex(storedHash))
            {
                string enteredHash = Sha256Hex(enteredPassword);
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(enteredHash.ToLowerInvariant()),
                    Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant()));
            }

            return false;
        }

        /// <summary>آیا هش نیاز به مهاجرت به الگوریتم جدید دارد؟</summary>
        public static bool NeedsRehash(string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash)) return true;
            return !storedHash.StartsWith("PBKDF2$", StringComparison.Ordinal);
        }

        private static string Sha256Hex(string password)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }
    }
}