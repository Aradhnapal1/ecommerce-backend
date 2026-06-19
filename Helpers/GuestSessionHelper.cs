using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Ecommerce_Backend.Helpers
{
    public static class GuestSessionHelper
    {
        private const string CookieName = "ecommerce_guest_session";

        public static string GetOrCreateGuestSessionId(HttpContext httpContext, IConfiguration configuration)
        {
            if (httpContext.Request.Cookies.TryGetValue(CookieName, out var existing) &&
                TryValidateSignedValue(existing, configuration, out var guestId))
            {
                return guestId;
            }

            var newGuestId = Guid.NewGuid().ToString("N");
            var signedValue = SignValue(newGuestId, configuration);

            httpContext.Response.Cookies.Append(CookieName, signedValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(30),
                IsEssential = true,
                Path = "/"
            });

            return newGuestId;
        }

        public static string? ResolveGuestIdentifier(HttpContext httpContext, IConfiguration configuration)
        {
            if (httpContext.Request.Cookies.TryGetValue(CookieName, out var existing) &&
                TryValidateSignedValue(existing, configuration, out var guestId))
            {
                return guestId;
            }

            return UserContextHelper.GetClientIp(httpContext);
        }

        private static string SignValue(string value, IConfiguration configuration)
        {
            var secret = GetSigningSecret(configuration);
            var signature = ComputeHmac(value, secret);
            return $"{value}.{signature}";
        }

        private static bool TryValidateSignedValue(string signedValue, IConfiguration configuration, out string value)
        {
            value = string.Empty;
            var parts = signedValue.Split('.', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                return false;

            var secret = GetSigningSecret(configuration);
            var expected = ComputeHmac(parts[0], secret);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(parts[1])))
            {
                return false;
            }

            value = parts[0];
            return true;
        }

        private static string ComputeHmac(string value, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(hash)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static string GetSigningSecret(IConfiguration configuration) =>
            configuration["GuestSession:SigningKey"]
            ?? configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Guest session signing key is not configured.");
    }
}
