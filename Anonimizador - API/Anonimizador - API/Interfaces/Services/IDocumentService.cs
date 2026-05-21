using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio principal de documentos.
    /// Orquesta la validación, procesamiento y auditoría del flujo de anonimización.
    /// </summary>
    public interface IDocumentService
    {
        /// <summary>
        /// Recibe un documento, lo anonimiza y retorna el resultado como stream.
        /// </summary>
        /// <param name="request">Archivo y datos de las personas a anonimizar.</param>
        /// <returns>Stream del documento anonimizado, nombre y tipo de contenido.</returns>
        Task<(Stream FileStream, string FileName, string ContentType)> UploadStreamAsync(
            UploadDocumentRequestDto request);

        /// <summary>
        /// Retorna el historial de documentos procesados para el dashboard.
        /// </summary>
        Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync();

        /// <summary>
        /// Retorna las métricas del sistema para el dashboard.
        /// </summary>
        Task<MetricsResponseDto> GetMetricsAsync();
    }
}