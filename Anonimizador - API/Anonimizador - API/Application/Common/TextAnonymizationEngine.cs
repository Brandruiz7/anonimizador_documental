using Anonimizador___API.Application.DTOs.Documents;

namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Motor de anonimización de texto reutilizable entre procesadores.
    /// Aplica reemplazos sobre texto plano y registra la auditoría de cada campo.
    ///
    /// Usado por <see cref="Processors.WordDocumentProcessor"/> para anonimizar
    /// el texto de párrafos, tablas, headers, footers y textboxes del DOCX.
    ///
    /// Etiquetas generadas por tipo de campo:
    /// - Datos personales:  [Px-Nombre], [Px-Cédula], [Px-Correo], [Px-Tel], [Px-Cargo], [Px-Dir], [Px-Institución]
    /// - Datos sensibles:   [Px-CuentaBancaria], [Px-CondiciónMédica]
    /// - Texto libre:       [Px-Dato]
    /// - Datos generales:   [Expediente], [N° Oficio]
    /// Donde x es el índice de persona (1-based).
    /// </summary>
    public static class TextAnonymizationEngine
    {
        /// <summary>
        /// Aplica todos los targets de anonimización sobre un texto de entrada.
        /// Procesa primero los datos personales, luego los sensibles y finalmente los generales.
        /// Los reemplazos son acumulativos — el texto resultante de un reemplazo
        /// es la entrada del siguiente.
        /// </summary>
        /// <param name="input">Texto original a anonimizar.</param>
        /// <param name="targets">Lista de datos sensibles a reemplazar.</param>
        /// <param name="auditFields">
        /// Acumulador de auditoría. Se agrega una entrada por cada valor único reemplazado.
        /// Si el mismo valor aparece en múltiples targets, se audita una sola vez.
        /// </param>
        /// <returns>Texto con los datos sensibles reemplazados por etiquetas neutrales.</returns>
        public static string ApplyTargets(
            string input,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            foreach (var target in targets)
            {
                // PersonIndex -1 = datos generales del documento (sin etiqueta de persona)
                // PersonIndex  0+ = datos de personas (etiqueta P1, P2, etc.)
                var label = $"P{target.PersonIndex + 1}";

                // Datos personales
                input = ReplaceAndAudit(input, target.FullName, $"[{label}-Nombre]", $"{label}-Nombre", auditFields);
                input = ReplaceAndAudit(input, target.Identification, $"[{label}-Cédula]", $"{label}-Cédula", auditFields);
                input = ReplaceAndAudit(input, target.Email, $"[{label}-Correo]", $"{label}-Correo", auditFields);
                input = ReplaceAndAudit(input, target.PhoneNumber, $"[{label}-Tel]", $"{label}-Tel", auditFields);
                input = ReplaceAndAudit(input, target.Position, $"[{label}-Cargo]", $"{label}-Cargo", auditFields);
                input = ReplaceAndAudit(input, target.Address, $"[{label}-Dir]", $"{label}-Dir", auditFields);
                input = ReplaceAndAudit(input, target.Institution, $"[{label}-Institución]", $"{label}-Institución", auditFields);

                // Datos sensibles PRODHAB
                input = ReplaceAndAudit(input, target.BankAccount, $"[{label}-CuentaBancaria]", $"{label}-CuentaBancaria", auditFields);
                input = ReplaceAndAudit(input, target.MedicalCondition, $"[{label}-CondiciónMédica]", $"{label}-CondiciónMédica", auditFields);

                // Texto libre — reemplazo exacto definido por el usuario
                input = ReplaceAndAudit(input, target.FreeText, $"[{label}-Dato]", $"{label}-Dato", auditFields);

                // Datos generales del documento — etiquetas fijas sin índice de persona
                input = ReplaceAndAudit(input, target.CaseNumber, "[Expediente]", "Expediente", auditFields);
                input = ReplaceAndAudit(input, target.OfficeNumber, "[N° Oficio]", "Oficio", auditFields);
            }

            return input;
        }

        /// <summary>
        /// Reemplaza todas las ocurrencias de un valor en el texto y registra la auditoría.
        /// El reemplazo es case-insensitive — "Juan" y "JUAN" se tratan igual.
        /// La auditoría se registra una sola vez por valor único aunque aparezca múltiples veces.
        /// </summary>
        /// <param name="input">Texto sobre el que se aplica el reemplazo.</param>
        /// <param name="originalValue">Valor a buscar y reemplazar. Si es null o vacío no hace nada.</param>
        /// <param name="replacement">Etiqueta neutral que reemplaza al valor original.</param>
        /// <param name="fieldType">Tipo de campo para la auditoría (ej. "P1-Nombre").</param>
        /// <param name="auditFields">Acumulador de auditoría.</param>
        /// <returns>Texto con el valor reemplazado, o el texto original si no se encontró.</returns>
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

            // Registrar auditoría una sola vez por valor único
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