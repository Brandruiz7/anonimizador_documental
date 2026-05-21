using Anonimizador___API.Application.DTOs;

namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Engine responsible for text anonymization logic.
    /// Reusable across Word, PDF, OCR and future processors.
    /// </summary>
    public static class TextAnonymizationEngine
    {
        /// <summary>
        /// Applies anonymization targets to a text input.
        /// </summary>
        /// <param name="input">Original text.</param>
        /// <param name="targets">Targets to anonymize.</param>
        /// <param name="auditFields">Audit accumulator.</param>
        /// <returns>Anonymized text.</returns>
        public static string ApplyTargets(
            string input,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var personLabel = $"P{target.PersonIndex + 1}";

                input = ReplaceAndAudit(
                    input, target.FullName,
                    $"[{personLabel}-Nombre]",
                    $"{personLabel}-Nombre",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Identification,
                    $"[{personLabel}-Cédula]",
                    $"{personLabel}-Cédula",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Email,
                    $"[{personLabel}-Correo]",
                    $"{personLabel}-Correo",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.PhoneNumber,
                    $"[{personLabel}-Tel]",
                    $"{personLabel}-Tel",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Position,
                    $"[{personLabel}-Cargo]",
                    $"{personLabel}-Cargo",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Address,
                    $"[{personLabel}-Dir]",
                    $"{personLabel}-Dir",
                    auditFields);
            }

            return input;
        }

        /// <summary>
        /// Replaces a value and registers audit information.
        /// </summary>
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
                a.OriginalValue.Equals(
                    originalValue,
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyAudited)
            {
                auditFields.Add(new AuditFieldDto
                {
                    FieldType = fieldType,
                    OriginalValue = originalValue,
                    AnonymizedValue = replacement
                });
            }

            return input.Replace(
                originalValue,
                replacement,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}