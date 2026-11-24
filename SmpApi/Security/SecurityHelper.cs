using System.Security.Cryptography;
using System.Text;

namespace SmpApi.Security
{
    public static class SecurityHelper
    {
        public static bool VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash)) return false;
            if (enteredPassword == null) enteredPassword = "";

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

            if (storedHash.Length == 64)
            {
                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(enteredPassword));
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(sb.ToString().ToLowerInvariant()),
                    Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant()));
            }

            return false;
        }
    }
}