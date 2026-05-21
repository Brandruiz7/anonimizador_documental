using Anonimizador___API.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de análisis de documentos con IA.
    /// </summary>
    public interface IDocumentAnalysisService
    {
        /// <summary>
        /// Analiza un documento y detecta datos sensibles usando IA + Regex.
        /// </summary>
        Task<DocumentAnalysisResultDto> AnalyzeAsync(
            IFormFile file,
            string? additionalContext = null);
    }
}