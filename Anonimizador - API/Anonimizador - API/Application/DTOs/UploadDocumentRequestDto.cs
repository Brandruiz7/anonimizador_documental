using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// DTO utilizado para recibir la solicitud de carga de documentos.
    /// </summary>
    public class UploadDocumentRequestDto
    {
        /// <summary>
        /// Archivo a anonimizar.
        /// </summary>
        public required IFormFile File { get; set; }

        /// <summary>
        /// Usuario que sube el archivo.
        /// </summary>
        public string UploadedBy { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Identification { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Position { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
    }
} 