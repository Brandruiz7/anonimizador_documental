using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<HomeController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Anonymize(
            string? UploadedBy, string? FullName, string? Identification,
            string? Email, string? PhoneNumber, string? Position, string? Address)
        {
            try
            {
                _logger.LogWarning("POST received");

                var documento = Request.Form.Files.GetFile("documento");

                _logger.LogWarning("File: {Name}", documento?.FileName ?? "NULL");

                TempData["Success"] = $"Archivo: {documento?.FileName ?? "ninguno"} | Size: {documento?.Length}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error: {Message}", ex.Message);
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}