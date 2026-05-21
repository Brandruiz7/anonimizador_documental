using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace Anonimizador___API.Application.Services.Processors
{
    /// <summary>
    /// Processor responsible for Word document anonymization.
    /// </summary>
    public class WordDocumentProcessor : IDocumentProcessor
    {
        private readonly ILogger<WordDocumentProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="WordDocumentProcessor"/> class.
        /// </summary>
        public WordDocumentProcessor(
            ILogger<WordDocumentProcessor> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool CanProcess(string extension)
        {
            return extension.Equals(
                ".docx",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public async Task<AnonymizationResultDto> ProcessAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets)
        {
            try
            {
                _logger.LogInformation(
                    "Starting Word document anonymization");

                var auditFields = new List<AuditFieldDto>();

                using var memoryStream = new MemoryStream();

                await memoryStream.WriteAsync(fileBytes);

                using (var document =
                    WordprocessingDocument.Open(memoryStream, true))
                {
                    var mainPart = document.MainDocumentPart;

                    if (mainPart?.Document?.Body == null)
                        throw new Exception("Invalid Word document");

                    ProcessContainer(
                        mainPart.Document.Body,
                        targets,
                        auditFields);

                    foreach (var headerPart in mainPart.HeaderParts)
                    {
                        if (headerPart.Header != null)
                        {
                            ProcessContainer(
                                headerPart.Header,
                                targets,
                                auditFields);
                        }
                    }

                    foreach (var footerPart in mainPart.FooterParts)
                    {
                        if (footerPart.Footer != null)
                        {
                            ProcessContainer(
                                footerPart.Footer,
                                targets,
                                auditFields);
                        }
                    }

                    ProcessTextBoxes(
                        mainPart,
                        targets,
                        auditFields);

                    ProcessTrackedChanges(
                        mainPart.Document.Body,
                        targets,
                        auditFields);

                    mainPart.Document.Save();
                }

                _logger.LogInformation(
                    "Word document anonymized successfully");

                return new AnonymizationResultDto
                {
                    FileBytes = memoryStream.ToArray(),
                    AuditFields = auditFields
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing Word document");

                throw;
            }
        }

        private void ProcessContainer(
            OpenXmlElement element,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var table in element.Descendants<Table>())
            {
                ProcessTable(table, targets, auditFields);
            }

            foreach (var paragraph in element.Descendants<Paragraph>())
            {
                var isInsideTable =
                    paragraph.Ancestors<TableCell>().Any();

                if (!isInsideTable)
                {
                    ProcessParagraph(
                        paragraph,
                        targets,
                        auditFields);
                }
            }
        }

        private void ProcessTable(
            Table table,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                foreach (var cell in row.Elements<TableCell>())
                {
                    foreach (var paragraph in cell.Elements<Paragraph>())
                    {
                        ProcessParagraph(
                            paragraph,
                            targets,
                            auditFields);
                    }
                }
            }
        }

        private void ProcessTextBoxes(
            MainDocumentPart mainPart,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            if (mainPart.Document?.Body == null)
                return;

            foreach (var textBox in mainPart.Document.Body
                .Descendants<DocumentFormat.OpenXml.Vml.TextBox>())
            {
                foreach (var paragraph in
                    textBox.Descendants<Paragraph>())
                {
                    ProcessParagraph(
                        paragraph,
                        targets,
                        auditFields);
                }
            }

            foreach (var element in mainPart.Document.Body
                .Descendants<OpenXmlElement>())
            {
                if (element.LocalName == "txbx" ||
                    element.LocalName == "linkedTxbx")
                {
                    foreach (var paragraph in
                        element.Descendants<Paragraph>())
                    {
                        ProcessParagraph(
                            paragraph,
                            targets,
                            auditFields);
                    }
                }
            }
        }

        private void ProcessTrackedChanges(
            OpenXmlElement element,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var tracked in element
                .Descendants<OpenXmlElement>()
                .Where(e =>
                    e.LocalName == "ins" ||
                    e.LocalName == "del"))
            {
                foreach (var text in tracked.Descendants<Text>())
                {
                    text.Text =
                        TextAnonymizationEngine.ApplyTargets(
                            text.Text,
                            targets,
                            auditFields);
                }
            }
        }

        private void ProcessParagraph(
            Paragraph paragraph,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            var textElements =
                paragraph.Descendants<Text>().ToList();

            if (!textElements.Any())
                return;

            var originalText = string.Concat(
                textElements.Select(t => t.Text));

            if (string.IsNullOrWhiteSpace(originalText))
                return;

            var anonymizedText =
                TextAnonymizationEngine.ApplyTargets(
                    originalText,
                    targets,
                    auditFields);

            if (originalText == anonymizedText)
                return;

            textElements.First().Text = anonymizedText;

            if (anonymizedText.StartsWith(" ") ||
                anonymizedText.EndsWith(" "))
            {
                textElements.First().Space =
                    SpaceProcessingModeValues.Preserve;
            }

            foreach (var text in textElements.Skip(1))
            {
                text.Text = string.Empty;
            }
        }
    }
}