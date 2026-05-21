using System.Net;
using System.Text.Json;

namespace Anonimizador___API.CrossCutting
{
    /// <summary>
    /// Middleware que captura excepciones no manejadas en el pipeline
    /// y retorna una respuesta JSON estructurada en lugar de un error genérico.
    /// En desarrollo incluye el stack trace; en producción solo el mensaje.
    /// </summary>
    public class ExceptionMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        /// <summary>
        /// Inicializa el middleware con sus dependencias.
        /// </summary>
        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Procesa el request y captura cualquier excepción no manejada.
        /// </summary>
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
                    "Excepción no manejada | CorrelationId: {CorrelationId} | {Message}",
                    correlationId,
                    ex.Message);

                await HandleExceptionAsync(context, ex, correlationId);
            }
        }

        /// <summary>
        /// Construye y escribe la respuesta JSON de error según el tipo de excepción.
        /// </summary>
        private async Task HandleExceptionAsync(
            HttpContext context,
            Exception ex,
            string correlationId)
        {
            context.Response.ContentType = "application/json";

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