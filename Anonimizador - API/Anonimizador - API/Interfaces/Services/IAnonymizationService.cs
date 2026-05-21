using Anonimizador___API.Application.DTOs.Documents;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de anonimización de documentos Word.
    /// </summary>
    public interface IAnonymizationService
    {
        /// <summary>
        /// Anonimiza un documento Word en memoria y retorna el resultado con auditoría.
        /// </summary>
        /// <param name="fileBytes">Bytes del documento original.</param>
        /// <param name="targets">Lista de datos sensibles a reemplazar.</param>
        /// <returns>Documento anonimizado y lista de campos reemplazados.</returns>
        Task<AnonymizationResultDto> AnonymizeAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets);
    }
}