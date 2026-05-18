namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// DTO utilizado para devolver el resultado del proceso de carga de un documento.
    /// Contiene información relevante sobre el estado de la operación realizada.
    /// </summary>
    public class UploadDocumentResponseDto
    {
        /// <summary>
        /// Identificador único del documento dentro del sistema.
        /// Permite consultar o relacionar el documento en futuras operaciones.
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Mensaje descriptivo del resultado del proceso.
        /// Puede indicar éxito o proporcionar información adicional sobre la operación realizada.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
