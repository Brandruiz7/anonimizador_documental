using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Anonimizador___API.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Anonimizador___API.Application.Services
{
    public class AnonymizationService : IAnonymizationService
    {
        private readonly ILogger<AnonymizationService> _logger;

        public AnonymizationService(ILogger<AnonymizationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Procesa un documento Word en memoria y retorna
        /// el documento anonimizado junto con la auditoría de campos.
        /// </summary>
        public async Task<AnonymizationResultDto> AnonymizeAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets)
        {
            try
            {
                _logger.LogInformation("Starting in-memory anonymization");

                // Lista acumuladora de campos reemplazados
                var auditFields = new List<AuditFieldDto>();

                using var memoryStream = new MemoryStream();
                await memoryStream.WriteAsync(fileBytes);

                using (var document = WordprocessingDocument.Open(memoryStream, true))
                {
                    var mainPart = document.MainDocumentPart;

                    if (mainPart?.Document?.Body == null)
                        throw new Exception("Invalid Word document");

                    ProcessContainer(mainPart.Document.Body, targets, auditFields);

                    foreach (var headerPart in mainPart.HeaderParts)
                        if (headerPart.Header != null)
                            ProcessContainer(headerPart.Header, targets, auditFields);

                    foreach (var footerPart in mainPart.FooterParts)
                        if (footerPart.Footer != null)
                            ProcessContainer(footerPart.Footer, targets, auditFields);

                    ProcessTextBoxes(mainPart, targets, auditFields);
                    ProcessTrackedChanges(mainPart.Document.Body, targets, auditFields);

                    mainPart.Document.Save();
                }

                _logger.LogInformation(
                    "Document anonymized successfully. Fields replaced: {Count}",
                    auditFields.Count);

                return new AnonymizationResultDto
                {
                    FileBytes = memoryStream.ToArray(),
                    AuditFields = auditFields
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during anonymization");
                throw;
            }
        }

        private void ProcessContainer(
            OpenXmlElement element,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var table in element.Descendants<Table>())
                ProcessTable(table, targets, auditFields);

            foreach (var paragraph in element.Descendants<Paragraph>())
            {
                var isInsideTable = paragraph.Ancestors<TableCell>().Any();
                if (!isInsideTable)
                    ProcessParagraph(paragraph, targets, auditFields);
            }
        }

        private void ProcessTable(
            Table table,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var row in table.Elements<TableRow>())
                foreach (var cell in row.Elements<TableCell>())
                    foreach (var paragraph in cell.Elements<Paragraph>())
                        ProcessParagraph(paragraph, targets, auditFields);
        }

        private void ProcessTextBoxes(
            MainDocumentPart mainPart,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            if (mainPart.Document?.Body == null) return;

            foreach (var textBox in mainPart.Document.Body
                .Descendants<DocumentFormat.OpenXml.Vml.TextBox>())
            {
                foreach (var paragraph in textBox.Descendants<Paragraph>())
                    ProcessParagraph(paragraph, targets, auditFields);
            }

            foreach (var element in mainPart.Document.Body.Descendants<OpenXmlElement>())
            {
                if (element.LocalName == "txbx" || element.LocalName == "linkedTxbx")
                    foreach (var paragraph in element.Descendants<Paragraph>())
                        ProcessParagraph(paragraph, targets, auditFields);
            }
        }

        private void ProcessTrackedChanges(
            OpenXmlElement element,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var tracked in element.Descendants<OpenXmlElement>()
                .Where(e => e.LocalName == "ins" || e.LocalName == "del"))
            {
                foreach (var text in tracked.Descendants<Text>())
                {
                    var original = text.Text;
                    text.Text = ApplyTargets(text.Text, targets, auditFields);
                }
            }
        }

        private void ProcessParagraph(
            Paragraph paragraph,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            var textElements = paragraph.Descendants<Text>().ToList();

            if (!textElements.Any()) return;

            var originalText = string.Concat(textElements.Select(t => t.Text));

            if (string.IsNullOrWhiteSpace(originalText)) return;

            var anonymizedText = ApplyTargets(originalText, targets, auditFields);

            if (originalText == anonymizedText) return;

            textElements.First().Text = anonymizedText;

            if (anonymizedText.StartsWith(" ") || anonymizedText.EndsWith(" "))
                textElements.First().Space = SpaceProcessingModeValues.Preserve;

            foreach (var text in textElements.Skip(1))
                text.Text = string.Empty;
        }

        /// <summary>
        /// Aplica reemplazos y acumula en auditFields cada campo que fue sustituido.
        /// Incluye el número de persona en la etiqueta de reemplazo.
        /// </summary>
        private string ApplyTargets( string input, List<AnonymizationTargetDto> targets, List<AuditFieldDto> auditFields)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var personLabel = $"Persona {i + 1}";

                input = ReplaceAndAudit(
                    input, target.FullName,
                    $"[{personLabel} - Nombre]",
                    $"Persona {i + 1} - Nombre",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Identification,
                    $"[{personLabel} - Cédula]",
                    $"Persona {i + 1} - Cédula",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Email,
                    $"[{personLabel} - Correo]",
                    $"Persona {i + 1} - Correo",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.PhoneNumber,
                    $"[{personLabel} - Teléfono]",
                    $"Persona {i + 1} - Teléfono",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Position,
                    $"[{personLabel} - Cargo]",
                    $"Persona {i + 1} - Cargo",
                    auditFields);

                input = ReplaceAndAudit(
                    input, target.Address,
                    $"[{personLabel} - Dirección]",
                    $"Persona {i + 1} - Dirección",
                    auditFields);
            }

            return input;
        }

        /// <summary>
        /// Reemplaza un valor en el texto y registra la auditoría solo si hubo cambio.
        /// </summary>
        private string ReplaceAndAudit(string input, string? originalValue, string replacement, string fieldType, List<AuditFieldDto> auditFields)
        {
            if (string.IsNullOrWhiteSpace(originalValue)) return input;

            if (!input.Contains(originalValue, StringComparison.OrdinalIgnoreCase))
                return input;

            // Solo registramos una vez por valor único para no duplicar auditoría
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

            return input.Replace(originalValue, replacement,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}