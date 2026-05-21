namespace Anonimizador___API.Application.DTOs.Documents
{
    /// <summary>
    /// Resumen de un documento procesado para el dashboard.
    /// Solo contiene metadata — sin datos sensibles.
    /// </summary>
    public class DocumentSummaryDto
    {
        /// <summary>Identificador único del documento.</summary>
        public int DocumentId { get; set; }

        /// <summary>Nombre original del archivo subido.</summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>Tamaño del archivo en kilobytes.</summary>
        public long FileSizeKB { get; set; }

        /// <summary>Usuario o departamento que subió el archivo.</summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>Fecha y hora en que se registró el proceso.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Estado actual del proceso (UPLOADED, ANONYMIZED, FAILED).</summary>
        public string Status { get; set; } = string.Empty;
    }
}