namespace Anonimizador___API.Application.DTOs.Documents
{
    /// <summary>
    /// Representa un dato sensible individual a anonimizar,
    /// junto con el índice de la persona a la que pertenece.
    /// Se construye internamente a partir de <see cref="UploadDocumentRequestDto"/>.
    /// </summary>
    public class AnonymizationTargetDto
    {
        /// <summary>
        /// Índice de la persona dentro del documento (base 0).
        /// Define la etiqueta de reemplazo: Persona 1, Persona 2, etc.
        /// </summary>
        public int PersonIndex { get; set; }

        /// <summary>Nombre completo de la persona.</summary>
        public string? FullName { get; set; }

        /// <summary>Número de identificación (cédula, pasaporte, etc.).</summary>
        public string? Identification { get; set; }

        /// <summary>Correo electrónico.</summary>
        public string? Email { get; set; }

        /// <summary>Número de teléfono.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Cargo o puesto de la persona.</summary>
        public string? Position { get; set; }

        /// <summary>Dirección física.</summary>
        public string? Address { get; set; }
    }
}