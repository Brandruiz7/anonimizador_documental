using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador principal de la aplicación.
    /// Muestra la vista del wizard de anonimización.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        /// <summary>
        /// Inicializa el controlador con sus dependencias.
        /// </summary>
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Muestra el wizard de anonimización de documentos.
        /// </summary>
        public IActionResult Index() => View();
    }
}