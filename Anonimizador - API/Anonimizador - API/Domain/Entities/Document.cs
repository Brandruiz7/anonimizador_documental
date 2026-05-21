namespace Anonimizador___API.Domain.Entities
{
    /// <summary>
    /// Representa un documento dentro del sistema.
    /// Modela la estructura de la tabla DOCUMENTS en la base de datos.
    /// No contiene lógica de negocio — es una entidad de solo datos.
    /// </summary>
    public class Document
    {
        /// <summary>Identificador único del documento.</summary>
        public int DocumentId { get; set; }

        /// <summary>Nombre original del archivo cargado por el usuario.</summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// Tipo MIME del archivo.
        /// Ejemplo: application/vnd.openxmlformats-officedocument.wordprocessingml.document
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>Tamaño del archivo en kilobytes.</summary>
        public long FileSizeKB { get; set; }

        /// <summary>
        /// Hash SHA256 del archivo original.
        /// Se usa para verificar integridad y detectar reprocesamiento.
        /// </summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>Usuario o departamento que cargó el documento.</summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>Fecha y hora de creación del registro.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Indica si el documento ya fue anonimizado.</summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Estado actual del documento en el flujo del sistema.
        /// 1=UPLOADED, 2=PROCESSING, 3=ANONYMIZED, 4=FAILED.
        /// </summary>
        public int CurrentStatusId { get; set; }
    }
}