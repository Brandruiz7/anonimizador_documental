using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Anonimizador___API.Application.DTOs.Documents
{
    /// <summary>
    /// DTO para recibir la solicitud de carga y anonimización de un documento.
    /// Soporta múltiples personas con sus variaciones y datos generales del documento.
    /// </summary>
    public class UploadDocumentRequestDto
    {
        /// <summary>
        /// Archivo a anonimizar. Formatos soportados: .docx, .pdf.
        /// Tamaño máximo: 100 MB (validado en DocumentService).
        /// </summary>
        [Required(ErrorMessage = "El archivo es requerido.")]
        public required IFormFile File { get; set; }

        /// <summary>
        /// Usuario o departamento que sube el archivo.
        /// Se toma automáticamente del claim Name del JWT — no es enviado por el cliente.
        /// </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary>
        /// Datos generales del documento a anonimizar.
        /// Número de expediente y número de oficio.
        /// </summary>
        public DocumentGeneralDataDto GeneralData { get; set; } = new();

        /// <summary>
        /// Lista de personas cuyos datos serán anonimizados.
        /// Debe contener al menos una persona con al menos un campo con valor.
        /// Validado en DocumentService.BuildAnonymizationTargets.
        /// </summary>
        [Required(ErrorMessage = "Debe proporcionar al menos una persona.")]
        public List<PersonTargetDto> Persons { get; set; } = new();
    }

    /// <summary>
    /// Datos generales del documento que aplican a nivel global,
    /// no a una persona específica.
    /// Ambos campos son opcionales — si están presentes se reemplazan por etiquetas fijas.
    /// </summary>
    public class DocumentGeneralDataDto
    {
        /// <summary>
        /// Número de expediente del documento.
        /// Será reemplazado por la etiqueta [Expediente].
        /// </summary>
        public string? CaseNumber { get; set; }

        /// <summary>
        /// Número de oficio del documento.
        /// Será reemplazado por la etiqueta [N° Oficio].
        /// </summary>
        public string? OfficeNumber { get; set; }
    }

    /// <summary>
    /// Representa una persona cuyos datos serán anonimizados en el documento.
    /// Incluye datos personales, datos sensibles PRODHAB y texto libre.
    /// Los campos son opcionales individualmente pero al menos uno debe tener valor.
    /// </summary>
    public class PersonTargetDto
    {
        // ── Datos personales ──────────────────────────────────────────────────

        /// <summary>Nombre completo de la persona.</summary>
        public string? FullName { get; set; }

        /// <summary>Número de identificación (cédula, pasaporte, DIMEX, etc.).</summary>
        public string? Identification { get; set; }

        /// <summary>Correo electrónico.</summary>
        public string? Email { get; set; }

        /// <summary>Número de teléfono.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Cargo o puesto de la persona en su institución.</summary>
        public string? Position { get; set; }

        /// <summary>Dirección física completa.</summary>
        public string? Address { get; set; }

        /// <summary>Institución u organización a la que pertenece la persona.</summary>
        public string? Institution { get; set; }

        // ── Datos sensibles PRODHAB ───────────────────────────────────────────

        /// <summary>
        /// Número de cuenta bancaria.
        /// Clasificado como dato sensible según la Ley PRODHAB.
        /// Será reemplazado por [Px-CuentaBancaria].
        /// </summary>
        public string? BankAccount { get; set; }

        /// <summary>
        /// Condición médica o diagnóstico clínico.
        /// Clasificado como dato sensible según la Ley PRODHAB.
        /// Será reemplazado por [Px-CondiciónMédica].
        /// </summary>
        public string? MedicalCondition { get; set; }

        // ── Texto libre ───────────────────────────────────────────────────────

        /// <summary>
        /// Texto exacto que debe anonimizarse en el documento.
        /// Útil para fragmentos que no encajan en los campos estándar.
        /// Será reemplazado por [Px-Dato].
        /// </summary>
        public string? FreeText { get; set; }

        // ── Variaciones ───────────────────────────────────────────────────────

        /// <summary>
        /// Variaciones del nombre que también deben anonimizarse.
        /// Ejemplos: "Mora Sandoval", "señor Ruiz", "Brandon".
        /// Se reemplazan con la misma etiqueta que el nombre principal.
        /// </summary>
        public List<string> NameVariations { get; set; } = new();

        /// <summary>
        /// Variaciones del número de cédula.
        /// Ejemplo: "123456789" como alternativa a "1-2345-6789".
        /// Se reemplazan con la misma etiqueta que la cédula principal.
        /// </summary>
        public List<string> IdVariations { get; set; } = new();

        /// <summary>
        /// Variaciones del número de teléfono.
        /// Ejemplo: "22345678" como alternativa a "2234-5678".
        /// Se reemplazan con la misma etiqueta que el teléfono principal.
        /// </summary>
        public List<string> PhoneVariations { get; set; } = new();
    }
}