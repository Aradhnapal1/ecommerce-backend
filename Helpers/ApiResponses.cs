using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Helpers
{
    public static class ApiResponses
    {
        public const string InternalErrorMessage = "An unexpected error occurred. Please try again later.";

        public static ObjectResult InternalError(Exception ex, ILogger? logger = null, string? context = null)
        {
            logger?.LogError(ex, context ?? "Unhandled API error");

            var isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);

            return new ObjectResult(new
            {
                success = false,
                message = isDevelopment ? ex.Message : InternalErrorMessage
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
