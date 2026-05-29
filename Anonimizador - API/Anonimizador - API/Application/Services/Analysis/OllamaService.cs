using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Anonimizador___API.Application.Services.Analysis
{
    /// <summary>
    /// Servicio de comunicación con Ollama — servidor de IA local.
    /// Envía prompts al modelo configurado mediante la API REST de Ollama
    /// y retorna la respuesta generada como texto plano.
    ///
    /// Requisitos:
    /// - Ollama corriendo en el servidor (ollama serve)
    /// - Modelo descargado (ollama pull mistral)
    /// - URL y modelo configurados en appsettings bajo la sección Ollama
    ///
    /// Registrado como Singleton en Program.cs porque HttpClient
    /// debe reutilizarse entre requests para evitar agotamiento de sockets.
    /// </summary>
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly ILogger<OllamaService> _logger;

        /// <summary>
        /// Inicializa el cliente HTTP apuntando al servidor Ollama configurado.
        /// Valida que la configuración esté completa al arrancar.
        /// </summary>
        public OllamaService(
            IConfiguration configuration,
            ILogger<OllamaService> logger)
        {
            _logger = logger;
            _model = configuration["Ollama:Model"] ?? "mistral";

            var baseUrl = configuration["Ollama:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Ollama:BaseUrl no está configurado en appsettings.");

            var timeoutSeconds = int.TryParse(
                configuration["Ollama:TimeoutSeconds"], out var t) ? t : 120;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        /// <summary>
        /// Envía un prompt al modelo Ollama y retorna la respuesta como texto plano.
        /// Mantiene la misma firma que <see cref="GeminiService.GenerateAsync"/>
        /// para ser intercambiable sin modificar <see cref="DocumentAnalysisService"/>.
        /// </summary>
        /// <param name="prompt">Instrucción completa a enviar al modelo.</param>
        /// <returns>Respuesta generada por el modelo en texto plano.</returns>
        /// <exception cref="HttpRequestException">Si Ollama no está disponible.</exception>
        public async Task<string> GenerateAsync(string prompt)
        {
            _logger.LogInformation(
                "Enviando prompt a Ollama | Modelo: {Model}", _model);

            var requestBody = new
            {
                model = _model,
                prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);

            var response = await _httpClient.PostAsync(
                "/api/generate",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

            return result.GetProperty("response").GetString()
                ?? string.Empty;
        }
    }
}