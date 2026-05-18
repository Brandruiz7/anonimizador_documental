using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Application.Services;
using Anonimizador___API.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___API.API.Controllers
{
    /// <summary>
    /// Controlador encargado de gestionar las operaciones relacionadas con documentos.
    /// Expone endpoints para la carga y procesamiento de archivos.
    /// </summary>
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _service;

        /// <summary>
        /// Constructor del controlador de documentos.
        /// </summary>
        /// <param name="service">
        /// Servicio de aplicación encargado de la lógica de negocio
        /// para el procesamiento de documentos.
        /// </param>
        public DocumentsController(IDocumentService service)
        {
            _service = service;
        }

        /// <summary>
        /// Uploads and anonymizes a document.
        /// </summary>
        /// <param name="request">
        /// Archivo y datos asociados enviados como multipart/form-data.
        /// </param>
        /// <returns>
        /// Resultado del proceso con el identificador del documento.
        /// </returns>
        /// <response code="200">Documento procesado correctamente</response>
        /// <response code="400">Solicitud inválida</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin,Operator")]
        [RequestSizeLimit(104857600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequestDto request)
        {
            var (fileStream, fileName, contentType) =
                await _service.UploadStreamAsync(request);

            return File(fileStream, contentType, fileName);
        }
    }
}