using GenerativeAI;

namespace Anonimizador___API.Application.Services.Analysis
{
    /// <summary>
    /// Servicio de comunicación con la API de Gemini (Google AI).
    /// Reemplaza a OllamaService para entornos en la nube o institucionales
    /// donde no se dispone de un servidor de IA local.
    ///
    /// Diferencias clave con Ollama:
    /// - No requiere instalación local — es una API REST externa de Google
    /// - Requiere una API Key configurada en appsettings
    /// - El modelo por defecto es gemini-1.5-flash (gratuito con límites)
    /// - Para producción institucional usar gemini-1.5-pro con licencia Google Workspace
    /// </summary>
    public class GeminiService
    {
        private readonly GenerativeModel _model;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _modelName;

        /// <summary>
        /// Inicializa el cliente de Gemini con la API Key y modelo configurados.
        /// </summary>
        public GeminiService(
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _logger = logger;
            _modelName = configuration["Gemini:Model"] ?? "gemini-2.0-flash";

            var apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException(
                    "La API Key de Gemini no está configurada en Gemini:ApiKey.");

            var googleAI = new GoogleAi(apiKey);
            _model = googleAI.CreateGenerativeModel(_modelName);
        }

        /// <summary>
        /// Envía un prompt a Gemini y retorna la respuesta como texto plano.
        /// Mantiene la misma firma que OllamaService.GenerateAsync()
        /// para ser intercambiable sin modificar DocumentAnalysisService.
        /// </summary>
        /// <param name="prompt">Instrucción a enviar al modelo.</param>
        /// <returns>Respuesta generada por Gemini.</returns>
        public async Task<string> GenerateAsync(string prompt)
        {
            _logger.LogInformation(
                "Enviando prompt a Gemini | Modelo: {Model}", _modelName);

            var response = await _model.GenerateContentAsync(prompt);

            return response.Text() ?? string.Empty;
        }
    }
}