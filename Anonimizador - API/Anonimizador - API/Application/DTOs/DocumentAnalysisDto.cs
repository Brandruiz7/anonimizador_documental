namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Request para analizar un documento con IA.
    /// </summary>
    public class DocumentAnalysisRequestDto
    {
        /// <summary>Archivo a analizar.</summary>
        public required IFormFile File { get; set; }

        /// <summary>
        /// Contexto adicional opcional que el usuario
        /// puede proveer para mejorar la detección.
        /// Ej: "también detecta cuentas bancarias y fechas"
        /// </summary>
        public string? AdditionalContext { get; set; }
    }

    /// <summary>
    /// Resultado del análisis de IA.
    /// </summary>
    public class DocumentAnalysisResultDto
    {
        /// <summary>
        /// Personas detectadas con sus datos sensibles.
        /// </summary>
        public List<DetectedPersonDto> DetectedPersons { get; set; } = new();

        /// <summary>
        /// Texto extraído del documento para vista previa.
        /// </summary>
        public string PreviewText { get; set; } = string.Empty;

        /// <summary>
        /// Datos adicionales detectados que no encajan
        /// en el modelo de persona (cuentas, fechas, etc.)
        /// </summary>
        public List<AdditionalDataDto> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Persona detectada por la IA con sus datos.
    /// </summary>
    public class DetectedPersonDto
    {
        public string? FullName { get; set; }
        public string? Identification { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Position { get; set; }
        public string? Address { get; set; }

        /// <summary>
        /// Variaciones del nombre detectadas por la IA.
        /// Ej: "Brandon", "señor Ruiz", "el licenciado"
        /// </summary>
        public List<string> NameVariations { get; set; } = new();
    }

    /// <summary>
    /// Dato sensible adicional detectado fuera del modelo de persona.
    /// </summary>
    public class AdditionalDataDto
    {
        /// <summary>Tipo detectado: BANK_ACCOUNT, DATE, DISEASE, etc.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Valor detectado.</summary>
        public string Value { get; set; } = string.Empty;
    }
}