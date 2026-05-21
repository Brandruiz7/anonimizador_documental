using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Anonimizador___Web.Models;

namespace Anonimizador___Web.Controllers
{
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

                // Llamadas en paralelo
                var documentsTask = client.GetAsync($"{apiUrl}/api/documents");
                var metricsTask = client.GetAsync($"{apiUrl}/api/documents/metrics");

                await Task.WhenAll(documentsTask, metricsTask);

                var documentsResponse = await documentsTask;
                var metricsResponse = await metricsTask;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Documentos
                var documents = new List<DocumentSummaryViewModel>();
                if (documentsResponse.IsSuccessStatusCode)
                {
                    var json = await documentsResponse.Content.ReadAsStringAsync();
                    documents = JsonSerializer.Deserialize<List<DocumentSummaryViewModel>>(
                        json, options) ?? new();
                }

                // Métricas
                var metrics = new MetricsViewModel();
                if (metricsResponse.IsSuccessStatusCode)
                {
                    var json = await metricsResponse.Content.ReadAsStringAsync();
                    metrics = JsonSerializer.Deserialize<MetricsViewModel>(
                        json, options) ?? new();
                }

                var viewModel = new DashboardViewModel
                {
                    Documents = documents,
                    Metrics = metrics
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                ViewBag.Error = "Error de conexión con el servicio.";
                return View(new DashboardViewModel());
            }
        }
    }
}
