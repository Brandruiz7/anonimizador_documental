namespace Anonimizador___API.Application.DTOs.Documents
{
    /// <summary>
    /// Resultado interno del proceso de anonimización.
    /// Contiene el documento procesado y el registro de campos reemplazados.
    /// </summary>
    public class AnonymizationResultDto
    {
        /// <summary>Bytes del documento anonimizado.</summary>
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();

        /// <summary>Lista de campos detectados y reemplazados durante el proceso.</summary>
        public List<AuditFieldDto> AuditFields { get; set; } = new();
    }

    /// <summary>
    /// Representa un campo individual que fue anonimizado.
    /// Se usa para auditoría y trazabilidad del proceso.
    /// </summary>
    public class AuditFieldDto
    {
        /// <summary>
        /// Tipo de campo anonimizado.
        /// Ejemplos: P1-Nombre, P1-Cédula, P2-Correo.
        /// </summary>
        public string FieldType { get; set; } = string.Empty;

        /// <summary>Valor original antes de la anonimización.</summary>
        public string OriginalValue { get; set; } = string.Empty;

        /// <summary>Etiqueta de reemplazo aplicada en el documento.</summary>
        public string AnonymizedValue { get; set; } = string.Empty;
    }
}