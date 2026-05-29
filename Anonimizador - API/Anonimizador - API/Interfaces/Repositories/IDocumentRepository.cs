using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;

namespace Anonimizador___API.Interfaces.Repositories
{
    /// <summary>
    /// Contrato para el acceso a datos de documentos, versiones y auditoría.
    /// Su implementación concreta es <see cref="Infrastructure.Repositories.DocumentRepository"/>.
    ///
    /// Todos los métodos se ejecutan mediante Stored Procedures en Oracle XE 21c.
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Registra un nuevo proceso de anonimización en estado PROCESSING (2).
        /// Debe llamarse antes de procesar el documento para garantizar trazabilidad
        /// incluso si el proceso falla — en ese caso se actualiza a FAILED.
        /// </summary>
        /// <param name="fileName">Nombre original del archivo.</param>
        /// <param name="contentType">Tipo MIME del archivo.</param>
        /// <param name="fileSizeKb">Tamaño del archivo en kilobytes.</param>
        /// <param name="hash">Hash SHA256 del archivo original para integridad.</param>
        /// <param name="uploadedBy">Usuario que subió el archivo (del claim JWT).</param>
        /// <returns>DocumentId generado por la secuencia de Oracle.</returns>
        Task<int> InsertDocumentProcessAsync(
            string fileName,
            string contentType,
            long fileSizeKb,
            string hash,
            string uploadedBy);

        /// <summary>
        /// Registra una versión del documento anonimizado con su hash SHA256.
        /// El hash permite verificar que el documento no fue alterado post-anonimización.
        /// </summary>
        /// <param name="documentId">Identificador del documento padre.</param>
        /// <param name="versionType">Tipo de versión: ORIGINAL o ANONYMIZED.</param>
        /// <param name="fileHash">Hash SHA256 del archivo de esta versión.</param>
        /// <returns>VersionId generado por la secuencia de Oracle.</returns>
        Task<int> InsertDocumentVersionAsync(
            int documentId,
            string versionType,
            string fileHash);

        /// <summary>
        /// Registra un campo anonimizado para auditoría granular.
        /// Se llama una vez por cada campo reemplazado en el documento.
        /// </summary>
        /// <param name="versionId">Identificador de la versión anonimizada.</param>
        /// <param name="fieldType">Tipo de campo: P1-Nombre, P2-Correo, Expediente, etc.</param>
        /// <param name="originalValue">Valor original antes de la anonimización.</param>
        /// <param name="anonymizedValue">Etiqueta de reemplazo aplicada en el documento.</param>
        Task InsertAuditFieldAsync(
            int versionId,
            string fieldType,
            string originalValue,
            string anonymizedValue);

        /// <summary>
        /// Actualiza el estado del proceso de un documento.
        /// Estados válidos: 2=PROCESSING, 3=ANONYMIZED, 4=FAILED.
        /// </summary>
        /// <param name="documentId">Identificador del documento.</param>
        /// <param name="statusId">Identificador del nuevo estado (ver tabla PROCESS_STATUS).</param>
        Task UpdateProcessStatusAsync(int documentId, int statusId);

        /// <summary>
        /// Retorna el historial completo de documentos procesados ordenado por fecha descendente.
        /// </summary>
        Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync();

        /// <summary>
        /// Retorna todas las métricas del sistema agrupadas para el dashboard.
        /// </summary>
        Task<MetricsResponseDto> GetMetricsAsync();
    }
}