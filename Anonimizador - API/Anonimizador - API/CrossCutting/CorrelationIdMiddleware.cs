namespace Anonimizador___API.CrossCutting
{
    /// <summary>
    /// Middleware que asigna un ID único de correlación a cada request.
    /// Permite rastrear el flujo completo en los logs aunque haya múltiples requests simultáneos.
    /// Si el cliente envía el header X-Correlation-ID se reutiliza; de lo contrario se genera uno nuevo.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        /// <summary>
        /// Inicializa el middleware con sus dependencias.
        /// </summary>
        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Procesa el request asignando y propagando el ID de correlación.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers[CorrelationIdHeader]
                .FirstOrDefault() ?? Guid.NewGuid().ToString();

            context.Items[CorrelationIdHeader] = correlationId;
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            _logger.LogInformation(
                "Request iniciado | CorrelationId: {CorrelationId} | {Method} {Path}",
                correlationId,
                context.Request.Method,
                context.Request.Path);

            await _next(context);

            _logger.LogInformation(
                "Request finalizado | CorrelationId: {CorrelationId} | StatusCode: {StatusCode}",
                correlationId,
                context.Response.StatusCode);
        }
    }
}