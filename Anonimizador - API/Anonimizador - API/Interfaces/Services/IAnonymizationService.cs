using Anonimizador___API.Application.DTOs;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de anonimización de documentos.
    /// </summary>
    public interface IAnonymizationService
    {
        /// <summary>
        /// Anonimiza un documento Word en memoria.
        /// Retorna el documento procesado y los campos que fueron reemplazados.
        /// </summary>
        Task<AnonymizationResultDto> AnonymizeAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets);
    }
}