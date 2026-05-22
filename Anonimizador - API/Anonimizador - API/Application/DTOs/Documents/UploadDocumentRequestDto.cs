using Microsoft.AspNetCore.Http;

namespace Anonimizador___API.Application.DTOs.Documents
{
    /// <summary>
    /// DTO para recibir la solicitud de carga y anonimización de un documento.
    /// Soporta múltiples personas y datos generales del documento.
    /// </summary>
    public class UploadDocumentRequestDto
    {
        /// <summary>Archivo a anonimizar. Formatos soportados: .docx, .pdf.</summary>
        public required IFormFile File { get; set; }

        /// <summary>
        /// Usuario o departamento que sube el archivo.
        /// Se toma automáticamente del claim del JWT.
        /// </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>
        /// Datos generales del documento a anonimizar.
        /// Número de expediente y número de oficio.
        /// </summary>
        public DocumentGeneralDataDto GeneralData { get; set; } = new();

        /// <summary>
        /// Lista de personas cuyos datos serán anonimizados.
        /// Debe contener al menos una persona con al menos un campo lleno.
        /// </summary>
        public List<PersonTargetDto> Persons { get; set; } = new();
    }

    /// <summary>
    /// Datos generales del documento que aplican a nivel global,
    /// no a una persona específica.
    /// </summary>
    public class DocumentGeneralDataDto
    {
        /// <summary>
        /// Número de expediente del documento.
        /// Será reemplazado por [Expediente].
        /// </summary>
        public string? CaseNumber { get; set; }

        /// <summary>
        /// Número de oficio del documento.
        /// Será reemplazado por [N° Oficio].
        /// </summary>
        public string? OfficeNumber { get; set; }
    }

    /// <summary>
    /// Representa una persona cuyos datos serán anonimizados.
    /// Incluye datos personales, datos sensibles y texto libre.
    /// </summary>
    public class PersonTargetDto
    {
        // ── Datos personales ──────────────────────────

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

        /// <summary>Institución a la que pertenece la persona.</summary>
        public string? Institution { get; set; }

        // ── Datos sensibles ─────────────────

        /// <summary>
        /// Número de cuenta bancaria.
        /// Será reemplazado por [Px-CuentaBancaria].
        /// </summary>
        public string? BankAccount { get; set; }

        /// <summary>
        /// Condición médica o diagnóstico.
        /// Será reemplazado por [Px-CondiciónMédica].
        /// </summary>
        public string? MedicalCondition { get; set; }

        // ── Texto libre ───────────────────────────────

        /// <summary>
        /// Texto exacto que debe anonimizarse en el documento.
        /// Útil para fragmentos que no encajan en los campos anteriores.
        /// Será reemplazado por [Px-Dato].
        /// </summary>
        public string? FreeText { get; set; }

        // ── Variaciones ───────────────────────────────

        /// <summary>
        /// Variaciones del nombre que también deben anonimizarse.
        /// Ejemplos: "Mora Sandoval", "señor Ruiz", "Brandon".
        /// </summary>
        public List<string> NameVariations { get; set; } = new();

        /// <summary>
        /// Variaciones del número de cédula.
        /// Ejemplo: "123456789" como alternativa a "1-2345-6789".
        /// </summary>
        public List<string> IdVariations { get; set; } = new();

        /// <summary>
        /// Variaciones del número de teléfono.
        /// Ejemplo: "22345678" como alternativa a "2234-5678".
        /// </summary>
        public List<string> PhoneVariations { get; set; } = new();
    }
}