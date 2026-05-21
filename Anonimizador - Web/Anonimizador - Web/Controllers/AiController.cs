using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Proxy hacia el endpoint de análisis IA de la API.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Route("ai")]
    public class AiController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiController> _logger;

        public AiController(
            IConfiguration configuration,
            ILogger<AiController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

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
                    return BadRequest("No file received");

                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var token = User.FindFirst("jwt_token")?.Value;

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

                // Timeout más largo para Ollama
                client.Timeout = TimeSpan.FromMinutes(3);

                using var content = new MultipartFormDataContent();

                var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(
                        file.ContentType);
                content.Add(fileContent, "File", file.FileName);

                var additionalContext = form["AdditionalContext"].ToString();
                if (!string.IsNullOrWhiteSpace(additionalContext))
                    content.Add(
                        new StringContent(additionalContext),
                        "AdditionalContext");

                var response = await client.PostAsync(
                    $"{apiUrl}/api/documents/analyze", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "API analyze error: {Status} {Body}",
                        response.StatusCode, error);
                    return StatusCode((int)response.StatusCode, error);
                }

                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI analyze proxy");
                return StatusCode(500, ex.Message);
            }
        }
    }
}