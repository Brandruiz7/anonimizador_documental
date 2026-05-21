using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Anonimizador___API.Application.Services.Analysis
{
    /// <summary>
    /// Servicio de comunicación con Ollama local.
    /// Envía prompts al modelo configurado y retorna la respuesta generada.
    /// </summary>
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly ILogger<OllamaService> _logger;

        /// <summary>
        /// Inicializa el cliente HTTP apuntando al servidor Ollama configurado.
        /// </summary>
        public OllamaService(
            IConfiguration configuration,
            ILogger<OllamaService> logger)
        {
            _logger = logger;
            _model = configuration["Ollama:Model"] ?? "mistral";

            var baseUrl = configuration["Ollama:BaseUrl"]
                ?? "http://localhost:11434";

            var timeoutSeconds = int.Parse(
                configuration["Ollama:TimeoutSeconds"] ?? "120");

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        /// <summary>
        /// Envía un prompt al modelo Ollama y retorna la respuesta como texto plano.
        /// </summary>
        /// <param name="prompt">Instrucción a enviar al modelo.</param>
        /// <returns>Respuesta generada por el modelo.</returns>
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