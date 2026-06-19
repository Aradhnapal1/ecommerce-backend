using System.Security.Cryptography;

namespace Ecommerce_Backend.Helpers
{
    public static class SecurityTokenHelper
    {
        public static string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        }

        public static string GenerateSecureToken(int byteLength = 32)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        public static bool IsExpired(DateTime? expiresAtUtc) =>
            !expiresAtUtc.HasValue || expiresAtUtc.Value <= DateTime.UtcNow;
    }
}
