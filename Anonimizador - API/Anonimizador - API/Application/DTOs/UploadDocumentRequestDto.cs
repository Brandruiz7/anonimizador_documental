using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// DTO utilizado para recibir la solicitud de carga de documentos.
    /// Soporta múltiples personas a anonimizar en un mismo documento.
    /// </summary>
    public class UploadDocumentRequestDto
    {
        /// <summary>
        /// Archivo .docx a anonimizar.
        /// </summary>
        public required IFormFile File { get; set; }

        /// <summary>
        /// Usuario o departamento que sube el archivo.
        /// </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>
        /// Lista de personas cuyos datos serán anonimizados en el documento.
        /// Debe contener al menos una persona.
        /// </summary>
        public List<PersonTargetDto> Persons { get; set; } = new();
    }

    /// <summary>
    /// Representa una persona cuyos datos serán anonimizados.
    /// Todos los campos son opcionales — se anonimiza solo lo que se proporcione.
    /// </summary>
    public class PersonTargetDto
    {
        /// <summary>
        /// Nombre completo de la persona.
        /// Será reemplazado por [NAME] en el documento.
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Número de identificación (cédula, pasaporte, etc.).
        /// Será reemplazado por [ID] en el documento.
        /// </summary>
        public string? Identification { get; set; }

        /// <summary>
        /// Correo electrónico.
        /// Será reemplazado por [EMAIL] en el documento.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Número de teléfono.
        /// Será reemplazado por [PHONE] en el documento.
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Cargo o puesto de la persona.
        /// Será reemplazado por [POSITION] en el documento.
        /// </summary>
        public string? Position { get; set; }

        /// <summary>
        /// Dirección física.
        /// Será reemplazado por [ADDRESS] en el documento.
        /// </summary>
        public string? Address { get; set; }
    }
}