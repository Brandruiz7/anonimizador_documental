using Anonimizador___API.Application.DTOs.Documents;

namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Motor de anonimización de texto reutilizable entre procesadores.
    /// Aplica reemplazos sobre texto plano y registra la auditoría de cada campo.
    /// </summary>
    public static class TextAnonymizationEngine
    {
        /// <summary>
        /// Aplica todos los targets de anonimización sobre un texto de entrada.
        /// Genera etiquetas con formato [Px-Campo] según el índice de persona.
        /// </summary>
        /// <param name="input">Texto original a anonimizar.</param>
        /// <param name="targets">Lista de datos sensibles a reemplazar.</param>
        /// <param name="auditFields">Acumulador de auditoría para registrar cada reemplazo.</param>
        /// <returns>Texto con los datos sensibles reemplazados por etiquetas.</returns>
        public static string ApplyTargets(
            string input,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            foreach (var target in targets)
            {
                var label = $"P{target.PersonIndex + 1}";

                // Datos personales
                input = ReplaceAndAudit(input, target.FullName, $"[{label}-Nombre]", $"{label}-Nombre", auditFields);
                input = ReplaceAndAudit(input, target.Identification, $"[{label}-Cédula]", $"{label}-Cédula", auditFields);
                input = ReplaceAndAudit(input, target.Email, $"[{label}-Correo]", $"{label}-Correo", auditFields);
                input = ReplaceAndAudit(input, target.PhoneNumber, $"[{label}-Tel]", $"{label}-Tel", auditFields);
                input = ReplaceAndAudit(input, target.Position, $"[{label}-Cargo]", $"{label}-Cargo", auditFields);
                input = ReplaceAndAudit(input, target.Address, $"[{label}-Dir]", $"{label}-Dir", auditFields);
                input = ReplaceAndAudit(input, target.Institution, $"[{label}-Institución]", $"{label}-Institución", auditFields);

                // Datos sensibles
                input = ReplaceAndAudit(input, target.BankAccount, $"[{label}-CuentaBancaria]", $"{label}-CuentaBancaria", auditFields);
                input = ReplaceAndAudit(input, target.MedicalCondition, $"[{label}-CondiciónMédica]", $"{label}-CondiciónMédica", auditFields);

                // Texto libre
                input = ReplaceAndAudit(input, target.FreeText, $"[{label}-Dato]", $"{label}-Dato", auditFields);

                // Datos generales del documento
                input = ReplaceAndAudit(input, target.CaseNumber, "[Expediente]", "Expediente", auditFields);
                input = ReplaceAndAudit(input, target.OfficeNumber, "[N° Oficio]", "Oficio", auditFields);
            }

            return input;
        }

        /// <summary>
        /// Reemplaza un valor en el texto y registra la auditoría si no fue registrado antes.
        /// Usa comparación sin distinción de mayúsculas.
        /// </summary>
        /// <param name="input">Texto sobre el que se aplica el reemplazo.</param>
        /// <param name="originalValue">Valor a buscar y reemplazar.</param>
        /// <param name="replacement">Etiqueta de reemplazo.</param>
        /// <param name="fieldType">Tipo de campo para auditoría.</param>
        /// <param name="auditFields">Acumulador de auditoría.</param>
        /// <returns>Texto con el valor reemplazado.</returns>
        private static string ReplaceAndAudit(
            string input,
            string? originalValue,
            string replacement,
            string fieldType,
            List<AuditFieldDto> auditFields)
        {
            if (string.IsNullOrWhiteSpace(originalValue))
                return input;

            if (!input.Contains(originalValue, StringComparison.OrdinalIgnoreCase))
                return input;

            var alreadyAudited = auditFields.Any(a =>
                a.FieldType == fieldType &&
                a.OriginalValue.Equals(originalValue, StringComparison.OrdinalIgnoreCase));

            if (!alreadyAudited)
            {
                auditFields.Add(new AuditFieldDto
                {
                    FieldType = fieldType,
                    OriginalValue = originalValue,
                    AnonymizedValue = replacement
                });
            }

            return input.Replace(originalValue, replacement, StringComparison.OrdinalIgnoreCase);
        }
    }
}