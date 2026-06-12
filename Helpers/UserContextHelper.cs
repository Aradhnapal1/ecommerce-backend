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

        public static string? GetClientIp(HttpContext httpContext) =>
            httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
