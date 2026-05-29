using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio principal de documentos.
    /// Su implementación concreta es <see cref="Services.Documents.DocumentService"/>.
    ///
    /// Orquesta el flujo completo: validación → hash → BD → procesamiento → auditoría.
    /// </summary>
    public interface IDocumentService
    {
        /// <summary>
        /// Recibe un documento, lo anonimiza en memoria y retorna el resultado como stream.
        /// Registra todo el proceso en BD incluyendo auditoría campo por campo.
        /// </summary>
        /// <param name="request">Archivo y datos de las personas a anonimizar.</param>
        /// <returns>
        /// Tupla con el stream del documento anonimizado, nombre de archivo y tipo de contenido.
        /// El stream debe ser cerrado por el llamador después de usarlo.
        /// </returns>
        Task<(Stream FileStream, string FileName, string ContentType)> UploadStreamAsync(
            UploadDocumentRequestDto request);

        /// <summary>
        /// Retorna el historial completo de documentos procesados para el dashboard.
        /// </summary>
        Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync();

        /// <summary>
        /// Retorna todas las métricas del sistema agrupadas para el dashboard.
        /// </summary>
        Task<MetricsResponseDto> GetMetricsAsync();
    }
}