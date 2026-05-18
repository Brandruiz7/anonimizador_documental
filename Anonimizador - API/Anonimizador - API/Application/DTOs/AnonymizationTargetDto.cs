namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Representa una entidad definida manualmente
    /// para anonimización.
    /// </summary>
    public class AnonymizationTargetDto
    {
        /// <summary>
        /// Nombre completo.
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Identificación.
        /// </summary>
        public string? Identification { get; set; }

        /// <summary>
        /// Correo electrónico.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Teléfono.
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Cargo o puesto.
        /// </summary>
        public string? Position { get; set; }

        /// <summary>
        /// Dirección.
        /// </summary>
        public string? Address { get; set; }
    }
}