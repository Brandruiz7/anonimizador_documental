using Anonimizador___API.Application.DTOs.Documents;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para procesadores de documentos por formato.
    /// Cada implementación maneja un tipo de archivo específico (.docx, .pdf).
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Indica si este procesador soporta la extensión de archivo dada.
        /// </summary>
        /// <param name="extension">Extensión del archivo. Ejemplo: ".docx", ".pdf".</param>
        /// <returns>True si el procesador puede manejar ese formato.</returns>
        bool CanProcess(string extension);

        /// <summary>
        /// Procesa y anonimiza un documento en memoria.
        /// </summary>
        /// <param name="fileBytes">Bytes del documento original.</param>
        /// <param name="targets">Lista de datos sensibles a reemplazar.</param>
        /// <returns>Documento anonimizado y lista de campos reemplazados para auditoría.</returns>
        Task<AnonymizationResultDto> ProcessAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets);
    }
}