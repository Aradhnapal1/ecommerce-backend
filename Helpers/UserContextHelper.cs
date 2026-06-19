using System.Security.Claims;

namespace Ecommerce_Backend.Helpers
{
    public static class UserContextHelper
    {
        public static int? GetUserId(ClaimsPrincipal user)
        {
            var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var userId) ? userId : null;
        }

        public static bool IsAdmin(ClaimsPrincipal user) =>
            user.IsInRole(AuthRoles.Admin);

        public static string? GetClientIp(HttpContext httpContext)
        {
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"]
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var clientIp = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(clientIp))
                    return NormalizeIp(clientIp);
            }

            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp))
                return NormalizeIp(realIp.Trim());

            var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(remoteIp))
                return null;

            return NormalizeIp(remoteIp);
        }

        private static string NormalizeIp(string ip) =>
            ip == "::1" ? "127.0.0.1" : ip;

        public static bool IsStrongPassword(string? password) =>
            !string.IsNullOrWhiteSpace(password) && password.Length >= 8;
    }
}
