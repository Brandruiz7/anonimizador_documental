using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Proxy hacia el endpoint de análisis con IA de la API.
    /// Reenvía el archivo al API y retorna el resultado de detección.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Route("ai")]
    public class AiController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiController> _logger;

        /// <summary>
        /// Inicializa el controlador con sus dependencias.
        /// </summary>
        public AiController(
            IConfiguration configuration,
            ILogger<AiController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Recibe un archivo y lo envía al API para análisis híbrido (Regex + IA).
        /// Retorna el resultado en formato JSON para consumo del wizard.
        /// </summary>
        [HttpPost("analyze")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(104857600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> Analyze()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var file = form.Files.GetFile("File");

                if (file == null || file.Length == 0)
                    return BadRequest("No se recibió ningún archivo.");

                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var token = User.FindFirst("jwt_token")?.Value;

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler)
                {
                    // Timeout extendido para permitir que Ollama procese el documento
                    Timeout = TimeSpan.FromMinutes(3)
                };

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var content = new MultipartFormDataContent();

                var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, "File", file.FileName);

                var additionalContext = form["AdditionalContext"].ToString();
                if (!string.IsNullOrWhiteSpace(additionalContext))
                    content.Add(new StringContent(additionalContext), "AdditionalContext");

                var response = await client.PostAsync(
                    $"{apiUrl}/api/documents/analyze", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Error en API de análisis: {Status} | {Body}",
                        response.StatusCode, error);
                    return StatusCode((int)response.StatusCode, error);
                }

                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en proxy de análisis IA");
                return StatusCode(500, ex.Message);
            }
        }
    }
}