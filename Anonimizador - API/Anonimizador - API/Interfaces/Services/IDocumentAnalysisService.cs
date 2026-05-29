using Anonimizador___API.Application.DTOs.Analysis;
using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de análisis híbrido de documentos.
    /// Su implementación concreta es <see cref="Services.Analysis.DocumentAnalysisService"/>.
    ///
    /// El análisis combina dos motores:
    /// 1. Regex — detección rápida y precisa de patrones conocidos (cédulas, correos, teléfonos)
    /// 2. IA    — detección semántica de entidades nombradas (Ollama/Mistral o Gemini)
    /// Los resultados de ambos se fusionan evitando duplicados.
    /// </summary>
    public interface IDocumentAnalysisService
    {
        /// <summary>
        /// Analiza un documento y retorna los datos sensibles detectados.
        /// No modifica ni persiste el documento — es solo análisis para revisión del usuario.
        /// </summary>
        /// <param name="file">Archivo a analizar. Formatos soportados: .docx, .pdf.</param>
        /// <param name="additionalContext">
        /// Instrucciones adicionales para el motor de IA.
        /// Ejemplo: "también detecta cuentas bancarias y fechas del expediente".
        /// </param>
        /// <returns>
        /// Personas detectadas con sus campos, vista previa del texto extraído
        /// y datos sensibles adicionales que no pertenecen a una persona específica.
        /// </returns>
        Task<DocumentAnalysisResultDto> AnalyzeAsync(
            IFormFile file,
            string? additionalContext = null);
    }
}