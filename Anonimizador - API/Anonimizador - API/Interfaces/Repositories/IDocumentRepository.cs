using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;

namespace Anonimizador___API.Interfaces.Repositories
{
    /// <summary>
    /// Contrato para el acceso a datos de documentos, versiones y auditoría.
    /// Su implementación concreta se encuentra en la capa Infrastructure.
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Registra un nuevo documento en estado PROCESSING.
        /// </summary>
        /// <param name="fileName">Nombre original del archivo.</param>
        /// <param name="contentType">Tipo MIME del archivo.</param>
        /// <param name="fileSizeKb">Tamaño del archivo en kilobytes.</param>
        /// <param name="hash">Hash SHA256 del archivo original.</param>
        /// <param name="uploadedBy">Usuario que subió el archivo.</param>
        /// <returns>Identificador del documento generado.</returns>
        Task<int> InsertDocumentProcessAsync(
            string fileName,
            string contentType,
            long fileSizeKb,
            string hash,
            string uploadedBy);

        /// <summary>
        /// Registra una versión del documento (ORIGINAL o ANONYMIZED).
        /// </summary>
        /// <param name="documentId">Identificador del documento.</param>
        /// <param name="versionType">Tipo de versión: ORIGINAL o ANONYMIZED.</param>
        /// <param name="fileHash">Hash SHA256 del archivo de esta versión.</param>
        /// <returns>Identificador de la versión generada.</returns>
        Task<int> InsertDocumentVersionAsync(
            int documentId,
            string versionType,
            string fileHash);

        /// <summary>
        /// Registra un campo anonimizado para auditoría y trazabilidad.
        /// </summary>
        /// <param name="versionId">Identificador de la versión anonimizada.</param>
        /// <param name="fieldType">Tipo de campo: P1-Nombre, P2-Correo, etc.</param>
        /// <param name="originalValue">Valor original antes de la anonimización.</param>
        /// <param name="anonymizedValue">Etiqueta de reemplazo aplicada.</param>
        Task InsertAuditFieldAsync(
            int versionId,
            string fieldType,
            string originalValue,
            string anonymizedValue);

        /// <summary>
        /// Actualiza el estado del proceso de un documento.
        /// </summary>
        /// <param name="documentId">Identificador del documento.</param>
        /// <param name="statusId">Identificador del nuevo estado.</param>
        Task UpdateProcessStatusAsync(int documentId, int statusId);

        /// <summary>
        /// Retorna el historial de documentos procesados para el dashboard.
        /// </summary>
        Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync();

        /// <summary>
        /// Retorna todas las métricas del sistema para el dashboard.
        /// </summary>
        Task<MetricsResponseDto> GetMetricsAsync();
    }
}