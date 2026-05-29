namespace Anonimizador___API.CrossCutting
{
    /// <summary>
    /// Middleware que asigna un ID único de correlación a cada request HTTP.
    /// Permite rastrear el flujo completo en los logs aunque haya múltiples requests simultáneos.
    ///
    /// Comportamiento:
    /// - Si el cliente envía el header X-Correlation-ID se reutiliza ese valor
    /// - Si no lo envía se genera un GUID nuevo
    /// - El ID se propaga en los items del contexto (para otros middlewares) y en el header de respuesta
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
        /// El ID queda disponible en <c>context.Items["X-Correlation-ID"]</c>
        /// para que otros middlewares y servicios lo incluyan en sus logs.
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