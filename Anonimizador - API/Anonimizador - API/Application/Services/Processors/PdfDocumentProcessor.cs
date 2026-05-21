using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace Anonimizador___API.Application.Services.Processors
{
    /// <summary>
    /// PDF anonymization processor using image-based redaction.
    /// Converts each page to image, applies redactions, and rebuilds the PDF.
    /// The original text layer is completely removed — no text is selectable.
    /// This is the most secure approach for restricted documents.
    /// </summary>
    public class PdfDocumentProcessor : IDocumentProcessor
    {
        private readonly ILogger<PdfDocumentProcessor> _logger;

        public PdfDocumentProcessor(
            ILogger<PdfDocumentProcessor> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool CanProcess(string extension)
        {
            return extension.Equals(
                ".pdf",
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
                    "Starting image-based PDF anonymization");

                var auditFields = new List<AuditFieldDto>();

                // =====================================
                // 1. EXTRAER UBICACIONES DE TEXTO
                // Usamos PdfPig para leer coordenadas
                // antes de renderizar a imagen
                // =====================================
                var redactions = BuildRedactions(
                    fileBytes,
                    targets,
                    auditFields);

                _logger.LogInformation(
                    "Found {Count} redaction(s)", redactions.Count);

                // =====================================
                // 2. RENDERIZAR PÁGINAS A IMAGEN
                // Cada página se convierte a SKBitmap
                // =====================================
                var pageImages = Conversion.ToImages(
                    fileBytes,
                    options: new RenderOptions(Dpi: 250));

                // =====================================
                // 3. APLICAR REDACCIONES SOBRE IMÁGENES
                // Pintamos rectángulos blancos con etiqueta
                // =====================================
                var redactedImages = new List<SKBitmap>();

                var pageList = pageImages.ToList();

                for (int pageIndex = 0; pageIndex < pageList.Count; pageIndex++)
                {
                    var pageNumber = pageIndex + 1;
                    var bitmap = pageList[pageIndex];

                    // Obtenemos las dimensiones reales de la página
                    // para escalar coordenadas PDF a píxeles
                    var pageRedactions = redactions
                        .Where(r => r.PageNumber == pageNumber)
                        .ToList();

                    if (pageRedactions.Count == 0)
                    {
                        redactedImages.Add(bitmap);
                        continue;
                    }

                    // Obtenemos dimensiones de página en puntos PDF
                    // para calcular el factor de escala
                    var (pdfWidth, pdfHeight) =
                        GetPageDimensions(fileBytes, pageNumber);

                    var scaleX = bitmap.Width / pdfWidth;
                    var scaleY = bitmap.Height / pdfHeight;

                    // Dibujamos sobre el bitmap
                    using var canvas = new SKCanvas(bitmap);

                    foreach (var redaction in pageRedactions)
                    {
                        // Convertir coordenadas PDF (origen abajo-izquierda)
                        // a coordenadas imagen (origen arriba-izquierda)
                        var pixelX = (float)(redaction.X * scaleX);
                        var pixelY = (float)((pdfHeight - redaction.Y - redaction.Height) * scaleY);
                        var pixelW = (float)(redaction.Width * scaleX);
                        var pixelH = (float)(redaction.Height * scaleY);

                        var margin = 4f;

                        var rect = new SKRect(
                            pixelX - margin,
                            pixelY - margin,
                            pixelX + pixelW + margin,
                            pixelY + pixelH + (margin * 3)); // ← margen extra abajo para cubrir texto

                        // Rectángulo gris de redacción — sin borde negro                        
                        using var grayPaint = new SKPaint
                        {
                            Color = new SKColor(0xf2, 0xf4, 0xff), // ← #f2f4ff
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };

                        canvas.DrawRect(rect, grayPaint);

                        // Texto más grande y visible
                        var fontSize = Math.Max(10f, Math.Min(14f, pixelH * 0.85f)); // ← más grande

                        using var typeface = SKTypeface.FromFamilyName("Arial");
                        using var textPaint = new SKPaint
                        {
                            Color = new SKColor(0x36, 0x49, 0x9b), // ← color primario #36499b
                            TextSize = fontSize,
                            Typeface = typeface,
                            IsAntialias = true,
                            TextAlign = SKTextAlign.Center,
                            FakeBoldText = true // ← bold para que se vea mejor
                        };

                        // Centrar horizontalmente y verticalmente
                        var textX = rect.MidX;
                        var textY = rect.MidY + (fontSize / 3f);

                        canvas.DrawText(
                            redaction.ReplacementText,
                            textX,
                            textY,
                            textPaint);
                    }

                    redactedImages.Add(bitmap);
                }

                // =====================================
                // 4. RECONSTRUIR PDF DESDE IMÁGENES
                // Cada imagen se inserta como página
                // No hay capa de texto — todo es imagen
                // =====================================
                var outputPdf = new PdfDocument();

                foreach (var bitmap in redactedImages)
                {
                    // Convertir SKBitmap a PNG en memoria
                    using var imageStream = new MemoryStream();

                    using var skImage = SKImage.FromBitmap(bitmap);
                    using var data = skImage.Encode(
                        SKEncodedImageFormat.Png, 95);

                    data.SaveTo(imageStream);
                    imageStream.Position = 0;

                    // Crear página en PdfSharp con las dimensiones de la imagen
                    var page = outputPdf.AddPage();

                    // Usamos 72 DPI como base para calcular tamaño de página
                    page.Width = PdfSharpCore.Drawing.XUnit.FromPoint(
                        bitmap.Width * 72.0 / 250.0);
                    page.Height = PdfSharpCore.Drawing.XUnit.FromPoint(
                        bitmap.Height * 72.0 / 250.0);

                    using var graphics =
                        XGraphics.FromPdfPage(page);

                    var xImage = XImage.FromStream(() => imageStream);

                    graphics.DrawImage(
                        xImage,
                        0, 0,
                        page.Width,
                        page.Height);
                }

                // =====================================
                // 5. GUARDAR PDF FINAL EN MEMORIA
                // =====================================
                using var outputStream = new MemoryStream();
                outputPdf.Save(outputStream);

                // Limpiar bitmaps
                foreach (var bmp in redactedImages)
                    bmp.Dispose();

                _logger.LogInformation(
                    "Image-based PDF anonymization completed successfully");

                return new AnonymizationResultDto
                {
                    FileBytes = outputStream.ToArray(),
                    AuditFields = auditFields
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during image-based PDF anonymization");

                throw;
            }
        }

        /// <summary>
        /// Gets the dimensions of a specific PDF page in points.
        /// </summary>
        private (double Width, double Height) GetPageDimensions(
            byte[] fileBytes,
            int pageNumber)
        {
            using var stream = new MemoryStream(fileBytes);
            using var pdf = PdfPigDocument.Open(stream);

            var page = pdf.GetPage(pageNumber);

            return (
                page.Width,
                page.Height
            );
        }

        /// <summary>
        /// Extracts text locations and builds redaction areas.
        /// </summary>
        private List<PdfRedactionInfo> BuildRedactions(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            var redactions = new List<PdfRedactionInfo>();
            var lines = ExtractLines(fileBytes);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var personLabel = $"Persona {i + 1}";

                var fieldMappings = new[]
                {
                    (target.FullName,       $"[{personLabel} - Nombre]",    $"{personLabel} - Nombre"),
                    (target.Identification, $"[{personLabel} - Cédula]",    $"{personLabel} - Cédula"),
                    (target.Email,          $"[{personLabel} - Correo]",    $"{personLabel} - Correo"),
                    (target.PhoneNumber,    $"[{personLabel} - Teléfono]",  $"{personLabel} - Teléfono"),
                    (target.Position,       $"[{personLabel} - Cargo]",     $"{personLabel} - Cargo"),
                    (target.Address,        $"[{personLabel} - Dirección]", $"{personLabel} - Dirección")
                };

                foreach (var (value, replacement, fieldType)
                    in fieldMappings)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    foreach (var line in lines)
                    {
                        if (!line.Text.Contains(
                            value,
                            StringComparison.OrdinalIgnoreCase))
                            continue;

                        var matchedWords =
                            FindWordsForPhrase(line.Words, value);

                        if (matchedWords.Count == 0)
                            continue;

                        var x = matchedWords.Min(w => w.X);
                        var y = matchedWords.Min(w => w.Y);
                        var right = matchedWords.Max(w => w.X + w.Width);
                        var top = matchedWords.Max(w => w.Y + w.Height);

                        redactions.Add(new PdfRedactionInfo
                        {
                            PageNumber = line.PageNumber,
                            OriginalText = value,
                            ReplacementText = replacement,
                            X = x,
                            Y = y,
                            Width = right - x,
                            Height = top - y
                        });

                        var alreadyAudited = auditFields.Any(a =>
                            a.FieldType == fieldType &&
                            a.OriginalValue.Equals(
                                value,
                                StringComparison.OrdinalIgnoreCase));

                        if (!alreadyAudited)
                        {
                            auditFields.Add(new AuditFieldDto
                            {
                                FieldType = fieldType,
                                OriginalValue = value,
                                AnonymizedValue = replacement
                            });
                        }
                    }
                }
            }

            return redactions;
        }

        /// <summary>
        /// Extracts text lines grouped by vertical position.
        /// </summary>
        private List<PdfLineInfo> ExtractLines(byte[] fileBytes)
        {
            var lines = new List<PdfLineInfo>();

            using var stream = new MemoryStream(fileBytes);
            using var pdf = PdfPigDocument.Open(stream);

            foreach (var page in pdf.GetPages())
            {
                var wordGroups = page.GetWords()
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                    .OrderByDescending(g => g.Key);

                foreach (var group in wordGroups)
                {
                    var wordsInLine = group
                        .OrderBy(w => w.BoundingBox.Left)
                        .ToList();

                    var lineText = string.Join(
                        " ",
                        wordsInLine.Select(w => w.Text));

                    var lineWords = wordsInLine
                        .Select(w => new PdfWordInfo
                        {
                            PageNumber = page.Number,
                            Text = w.Text,
                            X = w.BoundingBox.Left,
                            Y = w.BoundingBox.Bottom,
                            Width = w.BoundingBox.Width,
                            Height = w.BoundingBox.Height
                        })
                        .ToList();

                    lines.Add(new PdfLineInfo
                    {
                        PageNumber = page.Number,
                        Text = lineText,
                        Words = lineWords
                    });
                }
            }

            return lines;
        }

        /// <summary>
        /// Finds words that form a target phrase using sliding window.
        /// </summary>
        private List<PdfWordInfo> FindWordsForPhrase(
            List<PdfWordInfo> words,
            string phrase)
        {
            var phraseWords = phrase.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            for (int start = 0;
                start <= words.Count - phraseWords.Length;
                start++)
            {
                var window = words
                    .Skip(start)
                    .Take(phraseWords.Length)
                    .ToList();

                var windowText = string.Join(
                    " ",
                    window.Select(w => w.Text));

                if (windowText.Equals(
                    phrase,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return window;
                }
            }

            // Fallback — búsqueda parcial
            var matched = words
                .Where(w => phrase.Contains(
                    w.Text,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            return matched.Count >= phraseWords.Length
                ? matched
                : new List<PdfWordInfo>();
        }
    }
}