namespace Anonimizador___API.Domain.Entities
{
    /// <summary>
    /// Representa un documento dentro del sistema.
    /// 
    /// Esta entidad modela la información principal de un archivo cargado,
    /// incluyendo sus metadatos, estado de procesamiento y control de integridad.
    /// 
    /// No contiene lógica de negocio, únicamente define la estructura
    /// de datos que corresponde a la tabla DOCUMENTS en la base de datos.
    /// </summary>
    public class Document
    {
        /// <summary>
        /// Identificador único del documento.
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Nombre original del archivo cargado por el usuario.
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de contenido del archivo (MIME Type).
        /// Ejemplo: application/vnd.openxmlformats-officedocument.wordprocessingml.document
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Tamaño del archivo en kilobytes.
        /// </summary>
        public long FileSizeKB { get; set; }

        /// <summary>
        /// Hash SHA256 del archivo.
        /// 
        /// Se utiliza para:
        /// - Detectar documentos duplicados
        /// - Garantizar integridad del contenido
        /// </summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>
        /// Usuario que cargó el documento.
        /// </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora de creación del registro.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Indica si el documento ya fue procesado (anonimizado).
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Estado actual del documento dentro del flujo del sistema.
        /// 
        /// Este valor normalmente referencia a una tabla de estados (STATUS),
        /// permitiendo manejar diferentes etapas como:
        /// - Subido
        /// - Procesando
        /// - Procesado
        /// - Error
        /// </summary>
        public int CurrentStatusId { get; set; }
    }
}