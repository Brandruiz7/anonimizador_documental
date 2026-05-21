using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Anonimizador___API.Application.Services
{
    /// <summary>
    /// Servicio para comunicarse con Ollama local.
    /// </summary>
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly ILogger<OllamaService> _logger;

        public OllamaService(
            IConfiguration configuration,
            ILogger<OllamaService> logger)
        {
            _logger = logger;
            _model = configuration["Ollama:Model"] ?? "mistral";

            var baseUrl = configuration["Ollama:BaseUrl"]
                ?? "http://localhost:11434";

            var timeout = int.Parse(
                configuration["Ollama:TimeoutSeconds"] ?? "120");

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeout)
            };
        }

        /// <summary>
        /// Envía un prompt a Ollama y retorna la respuesta como string.
        /// </summary>
        public async Task<string> GenerateAsync(string prompt)
        {
            _logger.LogInformation(
                "Sending prompt to Ollama model: {Model}", _model);

            var requestBody = new
            {
                model = _model,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);

            var response = await _httpClient.PostAsync(
                "/api/generate",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

            return result
                .GetProperty("response")
                .GetString() ?? string.Empty;
        }
    }
}