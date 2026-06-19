using Microsoft.AspNetCore.Diagnostics;

namespace Ecommerce_Backend.Helpers
{
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ApiExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext context,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var message = _environment.IsDevelopment()
                    ? exception.Message
                    : ApiResponses.InternalErrorMessage;

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    new { success = false, message },
                    cancellationToken);
                return true;
            }

            context.Response.Redirect("/Home/Error");
            return true;
        }
    }
}
