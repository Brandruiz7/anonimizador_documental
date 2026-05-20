namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Representa un resumen de documento para el dashboard.
    /// Solo contiene metadata — sin datos sensibles.
    /// </summary>
    public class DocumentSummaryDto
    {
        /// <summary>Identificador del documento.</summary>
        public int DocumentId { get; set; }

        /// <summary>Nombre original del archivo.</summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>Tamaño del archivo en KB.</summary>
        public long FileSizeKB { get; set; }

        /// <summary>Usuario que subió el archivo.</summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>Fecha y hora de creación.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Estado actual del proceso.</summary>
        public string Status { get; set; } = string.Empty;
    }
}