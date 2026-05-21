using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PDFtoImage;
using SkiaSharp;
using System.Text;
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
            var allWordsByPage = ExtractAllWordsByPage(fileBytes);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var personLabel = $"P{target.PersonIndex + 1}";

                // Usamos una Lista en lugar de un Array para poder agregar dinámicamente las variaciones
                var fieldMappings = new List<(string Value, string Replacement, string FieldType)>
                {
                    (target.FullName,       $"[{personLabel}-Nombre]",   $"{personLabel}-Nombre"),
                    (target.Identification, $"[{personLabel}-Cédula]",   $"{personLabel}-Cédula"),
                    (target.Email,          $"[{personLabel}-Correo]",   $"{personLabel}-Correo"),
                    (target.PhoneNumber,    $"[{personLabel}-Tel]",      $"{personLabel}-Tel"),
                    (target.Position,       $"[{personLabel}-Cargo]",    $"{personLabel}-Cargo"),
                    (target.Address,        $"[{personLabel}-Dir]",      $"{personLabel}-Dir")
                };

                foreach (var (value, replacement, fieldType) in fieldMappings)
                {
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    bool isAddress = fieldType.Contains("Dir");
                    bool isName = fieldType.Contains("Nombre");

                    // 1. Buscar primero en líneas agrupadas (frase exacta)
                    foreach (var line in lines)
                    {
                        if (!line.Text.Contains(value, StringComparison.OrdinalIgnoreCase)) continue;

                        var matchedWords = FindWordsForPhrase(line.Words, value);
                        if (matchedWords.Count > 0)
                        {
                            AddRedaction(redactions, auditFields, matchedWords, line.PageNumber, value, replacement, fieldType);
                        }
                    }

                    var alreadyFound = redactions.Any(r => r.OriginalText.Equals(value, StringComparison.OrdinalIgnoreCase));

                    // 2. Si no encontró en líneas, buscar en todas las palabras 
                    // Esto captura Cédulas divididas y Direcciones multilínea
                    if (!alreadyFound)
                    {
                        foreach (var (pageNum, pageWords) in allWordsByPage)
                        {
                            var matchedWords = FindWordsForPhrase(pageWords, value);
                            if (matchedWords.Count > 0)
                            {
                                AddRedaction(redactions, auditFields, matchedWords, pageNum, value, replacement, fieldType);
                            }
                        }
                    }

                    // ¡CORRECCIÓN! Detenemos las direcciones aquí para que pasen por el paso 2, pero no por el paso 3.
                    if (isAddress) continue;

                    // 3. Búsqueda por palabra individual — SOLO para nombres que no se encontraron completos
                    alreadyFound = redactions.Any(r => r.OriginalText.Equals(value, StringComparison.OrdinalIgnoreCase));

                    if (!alreadyFound && isName && value.Contains(' '))
                    {
                        var nameWords = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var (pageNum, pageWords) in allWordsByPage)
                        {
                            foreach (var nameWord in nameWords)
                            {
                                if (nameWord.Length <= 3) continue;

                                // ¡CORRECCIÓN! Limpiamos comas o puntos pegados al nombre
                                var wordMatch = pageWords.FirstOrDefault(w =>
                                    w.Text.TrimEnd(',', '.', ';', ':').Equals(nameWord, StringComparison.OrdinalIgnoreCase));

                                if (wordMatch == null) continue;

                                var alreadyRedacted = redactions.Any(r =>
                                    r.PageNumber == pageNum &&
                                    Math.Abs(r.X - wordMatch.X) < 5 &&
                                    Math.Abs(r.Y - wordMatch.Y) < 5);

                                if (!alreadyRedacted)
                                {
                                    AddRedaction(redactions, auditFields, new List<PdfWordInfo> { wordMatch }, pageNum, nameWord, replacement, fieldType);
                                }
                            }
                        }
                    }
                }
            }

            // 4. POST-PROCESAMIENTO: Unir anonimizaciones adyacentes del mismo tipo
            return MergeAdjacentRedactions(redactions);
        }

        /// <summary>
        /// Combina las anonimizaciones que están muy cerca unas de otras y pertenecen a la misma etiqueta.
        /// Esto evita el efecto "[P1-Nombre] texto [P1-Nombre]".
        /// </summary>
        private List<PdfRedactionInfo> MergeAdjacentRedactions(List<PdfRedactionInfo> redactions)
        {
            if (redactions == null || redactions.Count <= 1) return redactions;

            // Ordenamos por página, luego por la altura (Y) y luego de izquierda a derecha (X)
            var sorted = redactions
                .OrderBy(r => r.PageNumber)
                .ThenBy(r => r.Y)
                .ThenBy(r => r.X)
                .ToList();

            var mergedList = new List<PdfRedactionInfo>();
            var current = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                // Definimos las tolerancias
                double verticalTolerance = 10.0; // Puntos de diferencia en Y para considerar que están en la misma línea
                double horizontalTolerance = 50.0; // Qué tan separadas pueden estar en X para unirlas

                bool isSamePage = current.PageNumber == next.PageNumber;

                // CORRECCIÓN: Usamos ReplacementText en lugar de FieldType, ya que es lo que tenemos en el objeto
                bool isSameFieldType = current.ReplacementText == next.ReplacementText;

                bool isSameLine = Math.Abs(current.Y - next.Y) <= verticalTolerance;

                // Verifica si la siguiente palabra empieza cerca de donde termina la actual
                bool isCloseHorizontally = (next.X - (current.X + current.Width)) <= horizontalTolerance;

                if (isSamePage && isSameFieldType && isSameLine && isCloseHorizontally)
                {
                    // FUSIONAR: Expandimos el ancho de 'current' para que abarque hasta el final de 'next'
                    current.Width = (next.X + next.Width) - current.X;

                    // Opcional: Concatenar el texto original para auditoría
                    current.OriginalText += " " + next.OriginalText;
                }
                else
                {
                    // No se pueden fusionar, guardamos el actual y avanzamos al siguiente
                    mergedList.Add(current);
                    current = next;
                }
            }

            // Agregar el último elemento que quedó en memoria
            mergedList.Add(current);

            return mergedList;
        }

        private void AddRedaction(
            List<PdfRedactionInfo> redactions,
            List<AuditFieldDto> auditFields,
            List<PdfWordInfo> matchedWords,
            int pageNumber,
            string value,
            string replacement,
            string fieldType)
        {
            if (matchedWords.Count == 0) return;

            // Ordenamos las palabras temporalmente de arriba a abajo y de izquierda a derecha
            var sortedWords = matchedWords
                .OrderByDescending(w => w.Y)
                .ThenBy(w => w.X)
                .ToList();

            // Agrupar las palabras que pertenezcan a la misma línea (tolerancia de 8 puntos en Y)
            var groups = new List<List<PdfWordInfo>>();
            var currentGroup = new List<PdfWordInfo> { sortedWords[0] };
            groups.Add(currentGroup);

            for (int i = 1; i < sortedWords.Count; i++)
            {
                var word = sortedWords[i];
                var lastWord = currentGroup.Last();

                if (Math.Abs(word.Y - lastWord.Y) <= 8.0)
                {
                    currentGroup.Add(word);
                }
                else
                {
                    // Salto de línea detectado, creamos un nuevo grupo (nueva cajita)
                    currentGroup = new List<PdfWordInfo> { word };
                    groups.Add(currentGroup);
                }
            }

            // Crear una caja de redacción por cada línea encontrada
            foreach (var group in groups)
            {
                var x = group.Min(w => w.X);
                var y = group.Min(w => w.Y);
                var right = group.Max(w => w.X + w.Width);
                var top = group.Max(w => w.Y + w.Height);

                redactions.Add(new PdfRedactionInfo
                {
                    PageNumber = pageNumber,
                    OriginalText = value,
                    ReplacementText = replacement,
                    X = x,
                    Y = y,
                    Width = right - x,
                    Height = top - y
                });
            }

            // La auditoría se hace una sola vez por el texto completo
            var alreadyAudited = auditFields.Any(a =>
                a.FieldType == fieldType &&
                a.OriginalValue.Equals(value, StringComparison.OrdinalIgnoreCase));

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

        /// <summary>
        /// Extrae todas las palabras agrupadas por página y forzando orden de lectura.
        /// </summary>
        private Dictionary<int, List<PdfWordInfo>> ExtractAllWordsByPage(
            byte[] fileBytes)
        {
            var result = new Dictionary<int, List<PdfWordInfo>>();

            using var stream = new MemoryStream(fileBytes);
            using var pdf = PdfPigDocument.Open(stream);

            foreach (var page in pdf.GetPages())
            {
                result[page.Number] = page.GetWords()
                    .Select(w => new PdfWordInfo
                    {
                        PageNumber = page.Number,
                        Text = w.Text,
                        X = w.BoundingBox.Left,
                        Y = w.BoundingBox.Bottom,
                        Width = w.BoundingBox.Width,
                        Height = w.BoundingBox.Height
                    })
                    // ORDEN DE LECTURA GARANTIZADO: 
                    // 1. Agrupamos por línea Y (tolerancia de 5pts) descendente
                    // 2. Ordenamos por X (izquierda a derecha)
                    .OrderByDescending(w => Math.Round(w.Y / 5.0) * 5.0)
                    .ThenBy(w => w.X)
                    .ToList();
            }

            return result;
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
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 2) * 2)
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
        /// Finds words that form a target phrase using an elastic sliding window.
        /// </summary>
        private List<PdfWordInfo> FindWordsForPhrase(
            List<PdfWordInfo> words,
            string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return new List<PdfWordInfo>();

            string Normalize(string text) =>
                text.Replace(" ", "")
                    .Replace("-", "")
                    .Replace("\u2013", "")
                    .Replace("\u2014", "")
                    .ToLowerInvariant();

            string normalizedPhrase = Normalize(phrase).TrimEnd(',', '.', ';', ':');

            // 1. BÚSQUEDA ELÁSTICA (Frases completas y saltos de línea)
            for (int start = 0; start < words.Count; start++)
            {
                var currentMatch = new List<PdfWordInfo>();
                var currentText = new StringBuilder();

                for (int end = start; end < words.Count; end++)
                {
                    currentMatch.Add(words[end]);
                    currentText.Append(words[end].Text);

                    var normalizedCurrent = Normalize(currentText.ToString());
                    var normalizedCurrentTrimmed = normalizedCurrent.TrimEnd(',', '.', ';', ':');

                    if (normalizedCurrentTrimmed == normalizedPhrase)
                    {
                        return currentMatch;
                    }

                    if (normalizedCurrent.Length > normalizedPhrase.Length + 5)
                    {
                        break;
                    }
                }
            }

            // 2. BÚSQUEDA FALLBACK (Palabras individuales exactas)
            var phraseWords = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (phraseWords.Length == 1)
            {
                var singleWord = Normalize(phraseWords[0]);

                var single = words.FirstOrDefault(w =>
                {
                    // Limpiamos todo rastro de puntuación alrededor de la palabra antes de comparar
                    var normW = Normalize(w.Text).Trim(',', '.', ';', ':', '(', ')', '"', '\'');

                    // Usamos == estricto para evitar que "Mora" atrape a "camora@empresa.cr"
                    return normW == singleWord;
                });

                if (single != null)
                {
                    return new List<PdfWordInfo> { single };
                }
            }

            return new List<PdfWordInfo>();
        }
    }
}