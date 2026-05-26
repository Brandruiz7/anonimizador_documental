using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    [Route("error")]
    public class ErrorController : Controller
    {
        [Route("{statusCode}")]
        public IActionResult Handle(int statusCode)
        {
            var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            ViewBag.StatusCode = statusCode;
            ViewBag.OriginalPath = feature?.OriginalPath ?? "ruta desconocida";

            return statusCode switch
            {
                404 => View("Error404"),
                403 => View("Error403"),
                _ => View("Error500")
            };
        }
    }
}