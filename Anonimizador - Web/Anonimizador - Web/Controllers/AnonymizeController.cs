using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador del wizard de anonimización de documentos.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AnonymizeController : Controller
    {
        /// <summary>
        /// Muestra el wizard de anonimización.
        /// </summary>
        public IActionResult Index() => View();
    }
}