using System.Net;
using System.Text.Json;

namespace Anonimizador___API.CrossCutting
{
    /// <summary>
    /// Middleware que captura excepciones no manejadas en el pipeline
    /// y retorna una respuesta JSON estructurada en lugar de un error genérico de ASP.NET.
    ///
    /// Mapeo de excepciones a códigos HTTP:
    /// - <see cref="ArgumentException"/> / <see cref="ArgumentNullException"/> → 400 Bad Request
    /// - <see cref="UnauthorizedAccessException"/>                             → 401 Unauthorized
    /// - <see cref="FileNotFoundException"/>                                   → 404 Not Found
    /// - Cualquier otra excepción                                              → 500 Internal Server Error
    ///
    /// En entorno Development incluye el stack trace en el campo "detail".
    /// En Production ese campo es null para no exponer información interna.
    /// </summary>
    public class ExceptionMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";

        // Opciones de serialización reutilizadas — evita recrearlas en cada request
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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
        /// El campo "detail" solo se incluye en Development para no exponer stack traces en producción.
        /// </summary>
        private async Task HandleExceptionAsync(
            HttpContext context,
            Exception ex,
            string correlationId)
        {
            context.Response.ContentType = "application/json";

            // ArgumentNullException hereda de ArgumentException pero se mapea explícitamente
            // para mayor claridad — ambas son errores de validación del cliente (400)
            context.Response.StatusCode = ex switch
            {
                ArgumentNullException => (int)HttpStatusCode.BadRequest,
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

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, JsonOptions));
        }
    }
}