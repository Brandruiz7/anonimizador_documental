using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace Anonimizador___API.Application.Services.Processors
{
    /// <summary>
    /// Procesador de documentos Word (.docx).
    /// Anonimiza texto en párrafos, tablas, cuadros de texto,
    /// encabezados, pies de página y cambios rastreados.
    /// </summary>
    public class WordDocumentProcessor : IDocumentProcessor
    {
        private readonly ILogger<WordDocumentProcessor> _logger;

        /// <summary>
        /// Inicializa el procesador con su logger.
        /// </summary>
        public WordDocumentProcessor(ILogger<WordDocumentProcessor> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool CanProcess(string extension) =>
            extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public async Task<AnonymizationResultDto> ProcessAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets)
        {
            try
            {
                _logger.LogInformation("Iniciando anonimización de documento Word");

                var auditFields = new List<AuditFieldDto>();

                using var memoryStream = new MemoryStream();
                await memoryStream.WriteAsync(fileBytes);

                using (var document = WordprocessingDocument.Open(memoryStream, true))
                {
                    var mainPart = document.MainDocumentPart
                        ?? throw new Exception("Documento Word inválido.");

                    if (mainPart.Document?.Body == null)
                        throw new Exception("El documento no tiene cuerpo.");

                    ProcessContainer(mainPart.Document.Body, targets, auditFields);

                    foreach (var header in mainPart.HeaderParts)
                        if (header.Header != null)
                            ProcessContainer(header.Header, targets, auditFields);

                    foreach (var footer in mainPart.FooterParts)
                        if (footer.Footer != null)
                            ProcessContainer(footer.Footer, targets, auditFields);

                    ProcessTextBoxes(mainPart, targets, auditFields);
                    ProcessTrackedChanges(mainPart.Document.Body, targets, auditFields);

                    mainPart.Document.Save();
                }

                _logger.LogInformation("Documento Word anonimizado correctamente");

                return new AnonymizationResultDto
                {
                    FileBytes = memoryStream.ToArray(),
                    AuditFields = auditFields
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar documento Word");
                throw;
            }
        }

        /// <summary>
        /// Procesa tablas y párrafos fuera de tablas en un contenedor XML.
        /// </summary>
        private void ProcessContainer(
            OpenXmlElement element,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var table in element.Descendants<Table>())
                ProcessTable(table, targets, auditFields);

            foreach (var paragraph in element.Descendants<Paragraph>())
                if (!paragraph.Ancestors<TableCell>().Any())
                    ProcessParagraph(paragraph, targets, auditFields);
        }

        /// <summary>
        /// Procesa todos los párrafos dentro de una tabla.
        /// </summary>
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

        /// <summary>
        /// Procesa cuadros de texto VML y Drawing dentro del documento.
        /// </summary>
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
                if (element.LocalName is "txbx" or "linkedTxbx")
                    foreach (var paragraph in element.Descendants<Paragraph>())
                        ProcessParagraph(paragraph, targets, auditFields);
            }
        }

        /// <summary>
        /// Procesa cambios rastreados (inserciones y eliminaciones) en el documento.
        /// </summary>
        private void ProcessTrackedChanges(
            OpenXmlElement element,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            foreach (var tracked in element.Descendants<OpenXmlElement>()
                .Where(e => e.LocalName is "ins" or "del"))
            {
                foreach (var text in tracked.Descendants<Text>())
                    text.Text = TextAnonymizationEngine.ApplyTargets(
                        text.Text, targets, auditFields);
            }
        }

        /// <summary>
        /// Procesa un párrafo reconstruyendo el texto de todos sus runs
        /// para detectar datos sensibles que atraviesen múltiples runs.
        /// </summary>
        private void ProcessParagraph(
            Paragraph paragraph,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            var textElements = paragraph.Descendants<Text>().ToList();

            if (!textElements.Any()) return;

            var originalText = string.Concat(textElements.Select(t => t.Text));

            if (string.IsNullOrWhiteSpace(originalText)) return;

            var anonymizedText = TextAnonymizationEngine.ApplyTargets(
                originalText, targets, auditFields);

            if (originalText == anonymizedText) return;

            textElements.First().Text = anonymizedText;

            if (anonymizedText.StartsWith(" ") || anonymizedText.EndsWith(" "))
                textElements.First().Space = SpaceProcessingModeValues.Preserve;

            foreach (var text in textElements.Skip(1))
                text.Text = string.Empty;
        }
    }
}