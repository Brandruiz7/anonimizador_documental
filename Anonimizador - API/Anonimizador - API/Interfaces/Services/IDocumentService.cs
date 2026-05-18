using Anonimizador___API.Application.DTOs;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Define operaciones relacionadas con documentos.
    /// </summary>
    public interface IDocumentService
    {
        Task<(Stream FileStream, string FileName, string ContentType)> UploadStreamAsync(UploadDocumentRequestDto request);
    }
}