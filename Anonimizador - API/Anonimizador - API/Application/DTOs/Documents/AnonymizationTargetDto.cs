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
        /// Define la etiqueta: P1, P2, etc.
        /// -1 indica datos generales del documento.
        /// </summary>
        public int PersonIndex { get; set; }

        // ── Datos personales ──────────────────────────

        /// <summary>Nombre completo.</summary>
        public string? FullName { get; set; }

        /// <summary>Número de identificación.</summary>
        public string? Identification { get; set; }

        /// <summary>Correo electrónico.</summary>
        public string? Email { get; set; }

        /// <summary>Número de teléfono.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Cargo o puesto.</summary>
        public string? Position { get; set; }

        /// <summary>Dirección física.</summary>
        public string? Address { get; set; }

        /// <summary>Institución a la que pertenece.</summary>
        public string? Institution { get; set; }

        // ── Datos sensibles (PRODHAB) ─────────────────

        /// <summary>Número de cuenta bancaria.</summary>
        public string? BankAccount { get; set; }

        /// <summary>Condición médica o diagnóstico.</summary>
        public string? MedicalCondition { get; set; }

        // ── Texto libre ───────────────────────────────

        /// <summary>Texto exacto a anonimizar.</summary>
        public string? FreeText { get; set; }

        // ── Datos generales del documento ─────────────

        /// <summary>Número de expediente.</summary>
        public string? CaseNumber { get; set; }

        /// <summary>Número de oficio.</summary>
        public string? OfficeNumber { get; set; }

        /// <summary>Variaciones del nombre.</summary>
        public List<string> NameVariations { get; set; } = new();

        /// <summary>Variaciones del número de cédula.</summary>
        public List<string> IdVariations { get; set; } = new();

        /// <summary>Variaciones del número de teléfono.</summary>
        public List<string> PhoneVariations { get; set; } = new();
    }
}