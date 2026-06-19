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
            var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(remoteIp))
                return null;

            if (remoteIp == "::1")
                return "127.0.0.1";

            return remoteIp;
        }

        public static bool IsStrongPassword(string? password) =>
            !string.IsNullOrWhiteSpace(password) && password.Length >= 8;
    }
}
