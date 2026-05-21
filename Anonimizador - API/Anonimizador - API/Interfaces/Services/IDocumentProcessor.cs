using Anonimizador___API.Application.DTOs;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Defines a document processor capable of anonymizing
    /// specific document formats.
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Determines whether the processor supports the file extension.
        /// </summary>
        /// <param name="extension">File extension.</param>
        /// <returns>True if supported.</returns>
        bool CanProcess(string extension);

        /// <summary>
        /// Processes and anonymizes a document stream.
        /// </summary>
        /// <param name="fileBytes">Document bytes.</param>
        /// <param name="targets">Anonymization targets.</param>
        /// <returns>Anonymization result.</returns>
        Task<AnonymizationResultDto> ProcessAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets);
    }
}
