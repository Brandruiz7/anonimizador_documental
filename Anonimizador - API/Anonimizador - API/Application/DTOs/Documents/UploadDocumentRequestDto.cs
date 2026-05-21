using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Application.DTOs.Documents
{
    /// <summary>
    /// DTO para recibir la solicitud de carga y anonimización de un documento.
    /// Soporta múltiples personas a anonimizar en un mismo documento.
    /// </summary>
    public class UploadDocumentRequestDto
    {
        /// <summary>
        /// Archivo a anonimizar. Formatos soportados: .docx, .pdf.
        /// </summary>
        public required IFormFile File { get; set; }

        /// <summary>
        /// Usuario o departamento que sube el archivo.
        /// Se toma automáticamente del claim del JWT.
        /// </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>
        /// Lista de personas cuyos datos serán anonimizados.
        /// Debe contener al menos una persona con al menos un campo lleno.
        /// </summary>
        public List<PersonTargetDto> Persons { get; set; } = new();
    }

    /// <summary>
    /// Representa una persona cuyos datos serán anonimizados en el documento.
    /// Todos los campos son opcionales — solo se anonimiza lo que se proporcione.
    /// </summary>
    public class PersonTargetDto
    {
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

        /// <summary>
        /// Variaciones del nombre que también deben anonimizarse.
        /// Ejemplos: "Mora Sandoval", "señor Ruiz", "Brandon".
        /// </summary>
        public List<string> NameVariations { get; set; } = new();
    }
}