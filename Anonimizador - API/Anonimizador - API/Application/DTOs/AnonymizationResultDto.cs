namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Resultado del proceso de anonimización.
    /// Contiene el documento procesado y los campos que fueron reemplazados.
    /// </summary>
    public class AnonymizationResultDto
    {
        /// <summary>
        /// Documento anonimizado en bytes.
        /// </summary>
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Lista de campos que fueron detectados y reemplazados.
        /// </summary>
        public List<AuditFieldDto> AuditFields { get; set; } = new();
    }

    /// <summary>
    /// Representa un campo individual que fue anonimizado.
    /// </summary>
    public class AuditFieldDto
    {
        /// <summary>
        /// Tipo de campo: NAME, ID, EMAIL, PHONE, POSITION, ADDRESS.
        /// </summary>
        public string FieldType { get; set; } = string.Empty;

        /// <summary>
        /// Valor original antes de la anonimización.
        /// </summary>
        public string OriginalValue { get; set; } = string.Empty;

        /// <summary>
        /// Valor de reemplazo aplicado.
        /// </summary>
        public string AnonymizedValue { get; set; } = string.Empty;
    }
}