using Anonimizador___API.Application.DTOs.Documents;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para procesadores de documentos por formato.
    /// Cada implementación maneja un tipo de archivo específico.
    ///
    /// Implementaciones disponibles:
    /// - <see cref="Services.Processors.WordDocumentProcessor"/> → .docx
    /// - <see cref="Services.Processors.PdfDocumentProcessor"/>  → .pdf
    ///
    /// El procesador correcto se selecciona en DocumentService mediante CanProcess().
    /// Todo el procesamiento ocurre en memoria — el documento nunca se escribe a disco.
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Indica si este procesador soporta la extensión de archivo dada.
        /// </summary>
        /// <param name="extension">Extensión en minúsculas. Ejemplo: ".docx", ".pdf".</param>
        /// <returns>True si este procesador puede manejar ese formato.</returns>
        bool CanProcess(string extension);

        /// <summary>
        /// Anonimiza un documento en memoria reemplazando los datos sensibles por etiquetas.
        /// </summary>
        /// <param name="fileBytes">Bytes del documento original en memoria.</param>
        /// <param name="targets">Lista de datos sensibles a reemplazar con sus etiquetas.</param>
        /// <returns>
        /// Documento anonimizado como bytes y lista de campos reemplazados para auditoría.
        /// </returns>
        Task<AnonymizationResultDto> ProcessAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets);
    }
}