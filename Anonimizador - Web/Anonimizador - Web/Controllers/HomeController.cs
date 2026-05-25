using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador del Home — experiencia de presentación interna.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Muestra el Home con la experiencia dark luxury.
        /// </summary>
        public IActionResult Index() => View();

        /// <summary>
        /// Landing page pública — no requiere autenticación.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("/landing")]
        public IActionResult Landing() => View();
    }
}