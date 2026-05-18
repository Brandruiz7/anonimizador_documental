using System.Net;
using System.Text.Json;

namespace Anonimizador___API.CrossCutting
{
    /// <summary>
    /// Middleware que captura cualquier excepción no manejada en el pipeline
    /// y devuelve una respuesta JSON estructurada en lugar de crashear.
    /// </summary>
    public class ExceptionMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var correlationId = context.Items[CorrelationIdHeader]?.ToString()
                    ?? "unknown";

                _logger.LogError(
                    ex,
                    "Unhandled exception | CorrelationId: {CorrelationId} | {Message}",
                    correlationId,
                    ex.Message);

                await HandleExceptionAsync(context, ex, correlationId);
            }
        }

        private async Task HandleExceptionAsync(
            HttpContext context,
            Exception ex,
            string correlationId)
        {
            context.Response.ContentType = "application/json";

            // Mapeamos tipos de excepción conocidos a sus códigos HTTP correspondientes
            context.Response.StatusCode = ex switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                FileNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var response = new
            {
                success = false,
                correlationId = correlationId,
                statusCode = context.Response.StatusCode,
                message = ex.Message,
                // En desarrollo mostramos el stack trace, en producción no
                detail = _env.IsDevelopment() ? ex.StackTrace : null
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}