namespace Anonimizador___API.CrossCutting
{
    /// <summary>
    /// Middleware que genera un ID único de correlación por cada request.
    /// Permite rastrear un flujo completo en los logs aunque haya múltiples requests simultáneos.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Si el cliente ya manda un correlation ID lo reutilizamos,
            // si no, generamos uno nuevo.
            var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                ?? Guid.NewGuid().ToString();

            // Lo guardamos en los Items del context para que
            // cualquier service/middleware posterior pueda leerlo.
            context.Items[CorrelationIdHeader] = correlationId;

            // Lo incluimos en la respuesta también, útil para el cliente.
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            _logger.LogInformation(
                "Request started | CorrelationId: {CorrelationId} | {Method} {Path}",
                correlationId,
                context.Request.Method,
                context.Request.Path);

            await _next(context);

            _logger.LogInformation(
                "Request finished | CorrelationId: {CorrelationId} | StatusCode: {StatusCode}",
                correlationId,
                context.Response.StatusCode);
        }
    }
}