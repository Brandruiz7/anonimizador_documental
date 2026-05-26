using Anonimizador___API.Application.DTOs.Analysis;
using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Anonimizador___API.API.Controllers
{
    /// <summary>
    /// Controlador de documentos.
    /// Expone endpoints para anonimización, historial, métricas y análisis con IA.
    /// </summary>
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    [EnableRateLimiting("documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _service;
        private readonly IDocumentAnalysisService _analysisService;

        /// <summary>
        /// Inicializa el controlador con sus dependencias.
        /// </summary>
        public DocumentsController(
            IDocumentService service,
            IDocumentAnalysisService analysisService)
        {
            _service = service;
            _analysisService = analysisService;
        }

        /// <summary>
        /// Recibe un documento, lo anonimiza y retorna el archivo procesado.
        /// </summary>
        /// <param name="request">Archivo y personas a anonimizar en formato multipart/form-data.</param>
        /// <returns>Archivo anonimizado listo para descargar.</returns>
        /// <response code="200">Documento anonimizado correctamente.</response>
        /// <response code="400">Solicitud inválida.</response>
        /// <response code="401">No autorizado.</response>
        /// <response code="500">Error interno del servidor.</response>
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

        /// <summary>
        /// Retorna el historial de documentos procesados para el dashboard.
        /// </summary>
        /// <returns>Lista de documentos con metadata básica.</returns>
        /// <response code="200">Historial retornado correctamente.</response>
        /// <response code="401">No autorizado.</response>
        [HttpGet]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> GetAll()
        {
            var documents = await _service.GetAllDocumentsAsync();
            return Ok(documents);
        }

        /// <summary>
        /// Retorna las métricas del sistema para el dashboard.
        /// </summary>
        /// <returns>Resumen, datos por mes, estado y usuario.</returns>
        /// <response code="200">Métricas retornadas correctamente.</response>
        /// <response code="401">No autorizado.</response>
        [HttpGet("metrics")]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> GetMetrics()
        {
            var metrics = await _service.GetMetricsAsync();
            return Ok(metrics);
        }

        /// <summary>
        /// Analiza un documento con detección híbrida (Regex + IA) y retorna
        /// los datos sensibles detectados para revisión del usuario.
        /// </summary>
        /// <param name="request">Archivo y contexto adicional opcional.</param>
        /// <returns>Personas detectadas, vista previa y datos adicionales.</returns>
        /// <response code="200">Análisis completado correctamente.</response>
        /// <response code="400">Archivo inválido.</response>
        /// <response code="401">No autorizado.</response>
        [HttpPost("analyze")]
        [Authorize(Roles = "Admin,Operator")]
        [RequestSizeLimit(104857600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> Analyze([FromForm] DocumentAnalysisRequestDto request)
        {
            var result = await _analysisService.AnalyzeAsync(
                request.File,
                request.AdditionalContext);

            return Ok(result);
        }
    }
}