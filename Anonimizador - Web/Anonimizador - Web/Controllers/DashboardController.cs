using Anonimizador___Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador del dashboard.
    /// Carga el historial de documentos y métricas desde el API.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardController> _logger;

        /// <summary>
        /// Inicializa el controlador con sus dependencias.
        /// </summary>
        public DashboardController(
            IConfiguration configuration,
            ILogger<DashboardController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Carga el historial de documentos y métricas en paralelo
        /// y los pasa a la vista del dashboard.
        /// </summary>
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

                // Llamadas en paralelo para reducir tiempo de carga
                var documentsTask = client.GetAsync($"{apiUrl}/api/documents");
                var metricsTask = client.GetAsync($"{apiUrl}/api/documents/metrics");

                await Task.WhenAll(documentsTask, metricsTask);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var documents = new List<DocumentSummaryViewModel>();
                if ((await documentsTask).IsSuccessStatusCode)
                {
                    var json = await (await documentsTask).Content.ReadAsStringAsync();
                    documents = JsonSerializer.Deserialize<List<DocumentSummaryViewModel>>(
                        json, options) ?? new();
                }

                var metrics = new MetricsViewModel();
                if ((await metricsTask).IsSuccessStatusCode)
                {
                    var json = await (await metricsTask).Content.ReadAsStringAsync();
                    metrics = JsonSerializer.Deserialize<MetricsViewModel>(
                        json, options) ?? new();
                }

                return View(new DashboardViewModel
                {
                    Documents = documents,
                    Metrics = metrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el dashboard");
                ViewBag.Error = "Error de conexión con el servicio.";
                return View(new DashboardViewModel());
            }
        }
    }
}