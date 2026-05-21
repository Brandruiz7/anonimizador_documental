using Anonimizador___API.Application.DTOs.Analysis;
using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de análisis híbrido de documentos con IA y Regex.
    /// </summary>
    public interface IDocumentAnalysisService
    {
        /// <summary>
        /// Analiza un documento y detecta datos sensibles usando Regex e IA (Ollama).
        /// </summary>
        /// <param name="file">Archivo a analizar. Formatos soportados: .docx, .pdf.</param>
        /// <param name="additionalContext">
        /// Contexto adicional para mejorar la detección.
        /// Ejemplo: "también detecta cuentas bancarias y fechas del expediente".
        /// </param>
        /// <returns>Personas detectadas, texto de vista previa y datos adicionales.</returns>
        Task<DocumentAnalysisResultDto> AnalyzeAsync(
            IFormFile file,
            string? additionalContext = null);
    }
}