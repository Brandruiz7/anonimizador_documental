using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Application.DTOs.Analysis
{
    /// <summary>
    /// DTO para solicitar el análisis de un documento con IA.
    /// </summary>
    public class DocumentAnalysisRequestDto
    {
        /// <summary>Archivo a analizar. Formatos soportados: .docx, .pdf.</summary>
        public required IFormFile File { get; set; }

        /// <summary>
        /// Contexto adicional para mejorar la detección.
        /// Ejemplo: "también detecta cuentas bancarias y fechas del expediente".
        /// </summary>
        public string? AdditionalContext { get; set; }
    }

    /// <summary>
    /// Resultado del análisis híbrido (Regex + IA) de un documento.
    /// </summary>
    public class DocumentAnalysisResultDto
    {
        /// <summary>Personas detectadas con sus datos sensibles.</summary>
        public List<DetectedPersonDto> DetectedPersons { get; set; } = new();

        /// <summary>
        /// Texto extraído del documento para mostrar en vista previa.
        /// Limitado a los primeros 3000 caracteres.
        /// </summary>
        public string PreviewText { get; set; } = string.Empty;

        /// <summary>
        /// Datos sensibles adicionales que no corresponden a una persona
        /// específica: cuentas bancarias, enfermedades, fechas, etc.
        /// </summary>
        public List<AdditionalDataDto> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Persona detectada por la IA con sus datos sensibles.
    /// </summary>
    public class DetectedPersonDto
    {
        /// <summary>Nombre completo tal como aparece en el documento.</summary>
        public string? FullName { get; set; }

        /// <summary>Número de identificación.</summary>
        public string? Identification { get; set; }

        /// <summary>Correo electrónico.</summary>
        public string? Email { get; set; }

        /// <summary>Número de teléfono.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Cargo o puesto.</summary>
        public string? Position { get; set; }

        /// <summary>Dirección física.</summary>
        public string? Address { get; set; }

        /// <summary>
        /// Formas abreviadas con que se menciona a la persona en el documento.
        /// Ejemplos: "Mora Sandoval", "Carlos", "señor Ruiz".
        /// </summary>
        public List<string> NameVariations { get; set; } = new();
    }

    /// <summary>
    /// Dato sensible adicional detectado fuera del modelo de persona.
    /// </summary>
    public class AdditionalDataDto
    {
        /// <summary>
        /// Tipo de dato detectado.
        /// Valores posibles: BANK_ACCOUNT, DISEASE, DATE, OTHER.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Valor exacto detectado en el documento.</summary>
        public string Value { get; set; } = string.Empty;
    }
}