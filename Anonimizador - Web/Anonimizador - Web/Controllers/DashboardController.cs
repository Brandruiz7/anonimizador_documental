using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador del dashboard — muestra el historial de documentos procesados.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IConfiguration configuration,
            ILogger<DashboardController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var token = User.FindFirst("jwt_token")?.Value;

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync($"{apiUrl}/api/documents");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API error: {Status}", response.StatusCode);
                    ViewBag.Error = "No se pudo cargar el historial de documentos.";
                    return View(new List<DocumentSummaryViewModel>());
                }

                var json = await response.Content.ReadAsStringAsync();

                var documents = JsonSerializer.Deserialize<List<DocumentSummaryViewModel>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<DocumentSummaryViewModel>();

                return View(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                ViewBag.Error = "Error de conexión con el servicio.";
                return View(new List<DocumentSummaryViewModel>());
            }
        }
    }

    /// <summary>
    /// ViewModel para el resumen de documento en el dashboard.
    /// </summary>
    public class DocumentSummaryViewModel
    {
        public int DocumentId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeKB { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}