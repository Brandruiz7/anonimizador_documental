using GenerativeAI;

namespace Anonimizador___API.Application.Services.Analysis
{
    /// <summary>
    /// Servicio de comunicación con la API de Gemini (Google AI).
    /// Alternativa a <see cref="OllamaService"/> para entornos en la nube
    /// o institucionales donde no se dispone de un servidor de IA local.
    ///
    /// Para activar Gemini en lugar de Ollama:
    /// 1. Descomentar GeminiService en DocumentAnalysisService y Program.cs
    /// 2. Comentar OllamaService en los mismos archivos
    /// 3. Configurar Gemini:ApiKey y Gemini:Model en appsettings
    ///
    /// Diferencias clave con Ollama:
    /// - No requiere instalación local — es una API REST externa de Google
    /// - Requiere API Key con cuota activa en Google Cloud Console
    /// - Modelo por defecto: gemini-2.0-flash (gratuito con límites por minuto)
    /// - Para producción institucional considerar gemini-1.5-pro con licencia
    /// </summary>
    public class GeminiService
    {
        private readonly GenerativeModel _model;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _modelName;

        /// <summary>
        /// Inicializa el cliente de Gemini con la API Key y modelo configurados.
        /// Lanza <see cref="InvalidOperationException"/> si falta la API Key.
        /// </summary>
        public GeminiService(
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _logger = logger;
            _modelName = configuration["Gemini:Model"] ?? "gemini-2.0-flash";

            var apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Gemini:ApiKey no está configurada en appsettings. " +
                    "Obtené una key en https://aistudio.google.com/apikey");

            var googleAI = new GoogleAi(apiKey);
            _model = googleAI.CreateGenerativeModel(_modelName);
        }

        /// <summary>
        /// Envía un prompt a Gemini y retorna la respuesta como texto plano.
        /// Mantiene la misma firma que <see cref="OllamaService.GenerateAsync"/>
        /// para ser intercambiable sin modificar <see cref="DocumentAnalysisService"/>.
        /// </summary>
        /// <param name="prompt">Instrucción completa a enviar al modelo.</param>
        /// <returns>Respuesta generada por Gemini en texto plano.</returns>
        public async Task<string> GenerateAsync(string prompt)
        {
            _logger.LogInformation(
                "Enviando prompt a Gemini | Modelo: {Model}", _modelName);

            var response = await _model.GenerateContentAsync(prompt);

            return response.Text() ?? string.Empty;
        }
    }
}