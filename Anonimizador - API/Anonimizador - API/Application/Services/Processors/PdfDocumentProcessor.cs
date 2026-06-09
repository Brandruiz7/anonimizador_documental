using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using System.Text;
using UglyToad.PdfPig.AcroForms.Fields;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace Anonimizador___API.Application.Services.Processors
{
    /// <summary>
    /// Procesador de documentos PDF mediante redacción basada en imagen.
    /// Convierte cada página a imagen, aplica las redacciones sobre los píxeles
    /// y reconstruye el PDF sin capa de texto — el contenido original no es seleccionable.
    /// Este enfoque es el más seguro para documentos restringidos.
    /// </summary>
    public class PdfDocumentProcessor : IDocumentProcessor
    {
        private readonly ILogger<PdfDocumentProcessor> _logger;

        /// <summary>
        /// Inicializa el procesador con su logger.
        /// </summary>
        public PdfDocumentProcessor(ILogger<PdfDocumentProcessor> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool CanProcess(string extension) =>
            extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public async Task<AnonymizationResultDto> ProcessAsync(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets)
        {
            try
            {
                _logger.LogInformation("Iniciando anonimización de PDF por imagen");

                var auditFields = new List<AuditFieldDto>();

                // 1. Detectar ubicaciones de texto sensible con PdfPig
                var redactions = BuildRedactions(fileBytes, targets, auditFields);

                _logger.LogInformation(
                    "Redacciones detectadas: {Count}", redactions.Count);

                // 2. Renderizar páginas a imagen con PDFtoImage a 250 DPI
                var pageImages = Conversion.ToImages(
                    fileBytes,
                    options: new RenderOptions(Dpi: 250));

                var pageList = pageImages.ToList();
                var redactedImages = new List<SKBitmap>();

                for (int pageIndex = 0; pageIndex < pageList.Count; pageIndex++)
                {
                    var pageNumber = pageIndex + 1;
                    var bitmap = pageList[pageIndex];
                    var pageRedactions = redactions
                        .Where(r => r.PageNumber == pageNumber)
                        .ToList();

                    if (pageRedactions.Count == 0)
                    {
                        redactedImages.Add(bitmap);
                        continue;
                    }

                    var (pdfWidth, pdfHeight) = GetPageDimensions(fileBytes, pageNumber);
                    var scaleX = bitmap.Width / pdfWidth;
                    var scaleY = bitmap.Height / pdfHeight;

                    // 3. Aplicar redacciones sobre el bitmap de cada página
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
                            pixelY + pixelH + (margin * 3));

                        // Fondo de redacción con color corporativo (#f2f4ff)
                        using var fillPaint = new SKPaint
                        {
                            Color = new SKColor(0xf2, 0xf4, 0xff),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawRect(rect, fillPaint);

                        // Etiqueta de reemplazo centrada en el rectángulo
                        var fontSize = Math.Max(10f, Math.Min(14f, pixelH * 0.85f));

                        using var typeface = SKTypeface.FromFamilyName("Arial");
                        using var textPaint = new SKPaint
                        {
                            Color = new SKColor(0x36, 0x49, 0x9b),
                            IsAntialias = true
                        };

                        using var font = new SKFont(typeface, fontSize)
                        {
                            Embolden = true
                        };

                        canvas.DrawText(
                            redaction.ReplacementText,
                            rect.MidX,
                            rect.MidY + (fontSize / 3f),
                            SKTextAlign.Center,
                            font,
                            textPaint);
                    }

                    redactedImages.Add(bitmap);
                }

                // 4. Reconstruir PDF desde imágenes con PdfSharp
                var outputPdf = new PdfDocument();

                foreach (var bitmap in redactedImages)
                {
                    using var imageStream = new MemoryStream();
                    using var skImage = SKImage.FromBitmap(bitmap);
                    using var data = skImage.Encode(SKEncodedImageFormat.Png, 95);

                    data.SaveTo(imageStream);
                    imageStream.Position = 0;

                    var page = outputPdf.AddPage();

                    // Factor de conversión de 250 DPI a puntos PDF (72 pt/inch)
                    page.Width = PdfSharpCore.Drawing.XUnit.FromPoint(bitmap.Width * 72.0 / 250.0);
                    page.Height = PdfSharpCore.Drawing.XUnit.FromPoint(bitmap.Height * 72.0 / 250.0);

                    using var graphics = XGraphics.FromPdfPage(page);
                    var xImage = XImage.FromStream(() => imageStream);

                    graphics.DrawImage(xImage, 0, 0, page.Width, page.Height);
                }

                // 5. Guardar PDF en memoria y retornar
                using var outputStream = new MemoryStream();
                outputPdf.Save(outputStream);

                foreach (var bmp in redactedImages)
                    bmp.Dispose();

                _logger.LogInformation("PDF anonimizado correctamente");

                return new AnonymizationResultDto
                {
                    FileBytes = outputStream.ToArray(),
                    AuditFields = auditFields
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la anonimización del PDF");
                throw;
            }
        }

        /// <summary>
        /// Obtiene las dimensiones en puntos de una página específica del PDF.
        /// </summary>
        private (double Width, double Height) GetPageDimensions(
            byte[] fileBytes, int pageNumber)
        {
            using var stream = new MemoryStream(fileBytes);
            using var pdf = PdfPigDocument.Open(stream);
            var page = pdf.GetPage(pageNumber);
            return (page.Width, page.Height);
        }

        /// <summary>
        /// Construye la lista de redacciones detectando los datos sensibles en el PDF.
        /// Aplica búsqueda en tres niveles: líneas agrupadas, palabras globales y palabra individual.
        /// Para PDFs con campos AcroForm (formularios rellenables), extrae los valores
        /// directamente desde los campos y los agrega al pool de palabras antes de buscar.
        /// </summary>
        private List<PdfRedactionInfo> BuildRedactions(
            byte[] fileBytes,
            List<AnonymizationTargetDto> targets,
            List<AuditFieldDto> auditFields)
        {
            var redactions = new List<PdfRedactionInfo>();
            var lines = ExtractLines(fileBytes);
            var allWordsByPage = ExtractAllWordsByPage(fileBytes);

            // Extraer palabras de campos AcroForm e inyectarlas en el pool
            // de palabras y líneas para que BuildRedactions las encuentre igual
            // que cualquier otro texto del documento
            var acroWords = ExtractAcroFormWords(fileBytes);

            foreach (var (pageNum, words) in acroWords)
            {
                // Agregar al diccionario de palabras por página
                if (allWordsByPage.TryGetValue(pageNum, out var existingWords))
                    existingWords.AddRange(words);
                else
                    allWordsByPage[pageNum] = words;

                // Agregar como líneas independientes para que el Nivel 1 también las encuentre
                foreach (var word in words)
                {
                    lines.Add(new PdfLineInfo
                    {
                        PageNumber = pageNum,
                        Text = word.Text,
                        Words = new List<PdfWordInfo> { word }
                    });
                }
            }

            foreach (var target in targets)
            {
                var label = $"P{target.PersonIndex + 1}";

                var fieldMappings = new List<(string? Value, string Replacement, string FieldType)>
                {
                    // Datos personales
                    (target.FullName,         $"[{label}-Nombre]",         $"{label}-Nombre"),
                    (target.Identification,   $"[{label}-Cédula]",          $"{label}-Cédula"),
                    (target.Email,            $"[{label}-Correo]",          $"{label}-Correo"),
                    (target.PhoneNumber,      $"[{label}-Tel]",             $"{label}-Tel"),
                    (target.Position,         $"[{label}-Cargo]",           $"{label}-Cargo"),
                    (target.Address,          $"[{label}-Dir]",             $"{label}-Dir"),
                    (target.Institution,      $"[{label}-Institución]",     $"{label}-Institución"),
 
                    // Datos sensibles
                    (target.BankAccount,      $"[{label}-CuentaBancaria]",  $"{label}-CuentaBancaria"),
                    (target.MedicalCondition, $"[{label}-CondiciónMédica]", $"{label}-CondiciónMédica"),
 
                    // Texto libre
                    (target.FreeText,         $"[{label}-Dato]",            $"{label}-Dato"),
 
                    // Datos generales del documento
                    (target.CaseNumber,       "[Expediente]",               "Expediente"),
                    (target.OfficeNumber,     "[N° Oficio]",                "Oficio")
                };

                foreach (var (value, replacement, fieldType) in fieldMappings)
                {
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    var isAddress = fieldType.Contains("Dir");
                    var isName = fieldType.Contains("Nombre");
                    var isSensitive = fieldType.Contains("CuentaBancaria") ||
                                      fieldType.Contains("CondiciónMédica") ||
                                      fieldType.Contains("Expediente") ||
                                      fieldType.Contains("Oficio");

                    // Nivel 1: búsqueda en líneas agrupadas (frase exacta)
                    foreach (var line in lines)
                    {
                        if (!line.Text.Contains(value, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var matched = FindWordsForPhrase(line.Words, value);
                        if (matched.Count > 0)
                            AddRedaction(redactions, auditFields, matched,
                                line.PageNumber, value, replacement, fieldType);
                    }

                    var found = redactions.Any(r =>
                        r.OriginalText.Equals(value, StringComparison.OrdinalIgnoreCase));

                    // Nivel 2: búsqueda en todas las palabras de la página
                    if (!found)
                    {
                        foreach (var (pageNum, pageWords) in allWordsByPage)
                        {
                            var matched = FindWordsForPhrase(pageWords, value);
                            if (matched.Count > 0)
                                AddRedaction(redactions, auditFields, matched,
                                    pageNum, value, replacement, fieldType);
                        }
                    }

                    if (isAddress || isSensitive) continue;

                    found = redactions.Any(r =>
                        r.OriginalText.Equals(value, StringComparison.OrdinalIgnoreCase));

                    // Nivel 3: búsqueda por palabra individual — solo para nombres
                    if (!found && isName && value.Contains(' '))
                    {
                        var nameWords = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var (pageNum, pageWords) in allWordsByPage)
                        {
                            foreach (var nameWord in nameWords)
                            {
                                if (nameWord.Length <= 3) continue;

                                var wordMatch = pageWords.FirstOrDefault(w =>
                                    w.Text.TrimEnd(',', '.', ';', ':')
                                         .Equals(nameWord, StringComparison.OrdinalIgnoreCase));

                                if (wordMatch == null) continue;

                                var alreadyRedacted = redactions.Any(r =>
                                    r.PageNumber == pageNum &&
                                    Math.Abs(r.X - wordMatch.X) < 5 &&
                                    Math.Abs(r.Y - wordMatch.Y) < 5);

                                if (!alreadyRedacted)
                                    AddRedaction(redactions, auditFields,
                                        new List<PdfWordInfo> { wordMatch },
                                        pageNum, nameWord, replacement, fieldType);
                            }
                        }
                    }
                }
            }

            return MergeAdjacentRedactions(redactions);
        }

        /// <summary>
        /// Fusiona redacciones adyacentes de la misma etiqueta en la misma línea.
        /// Evita el efecto de múltiples cuadros separados para un mismo dato.
        /// </summary>
        private List<PdfRedactionInfo> MergeAdjacentRedactions(List<PdfRedactionInfo> redactions)
        {
            if (redactions.Count <= 1) return redactions;

            var sorted = redactions
                .OrderBy(r => r.PageNumber)
                .ThenBy(r => r.Y)
                .ThenBy(r => r.X)
                .ToList();

            var merged = new List<PdfRedactionInfo>();
            var current = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                var isSamePage = current.PageNumber == next.PageNumber;
                var isSameLabel = current.ReplacementText == next.ReplacementText;
                var isSameLine = Math.Abs(current.Y - next.Y) <= 10.0;
                var isCloseEnough = (next.X - (current.X + current.Width)) <= 50.0;

                if (isSamePage && isSameLabel && isSameLine && isCloseEnough)
                {
                    // Expandir el rectángulo actual para cubrir el siguiente
                    current.Width = (next.X + next.Width) - current.X;
                    current.OriginalText += " " + next.OriginalText;
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }

            merged.Add(current);
            return merged;
        }

        /// <summary>
        /// Agrega una o varias áreas de redacción agrupando las palabras por línea.
        /// Registra la auditoría del campo una sola vez por valor único.
        /// </summary>
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

            // Agrupar palabras por línea (tolerancia de 8 puntos en Y)
            var sorted = matchedWords.OrderByDescending(w => w.Y).ThenBy(w => w.X).ToList();
            var groups = new List<List<PdfWordInfo>>();
            var current = new List<PdfWordInfo> { sorted[0] };
            groups.Add(current);

            for (int i = 1; i < sorted.Count; i++)
            {
                if (Math.Abs(sorted[i].Y - current.Last().Y) <= 8.0)
                    current.Add(sorted[i]);
                else
                {
                    current = new List<PdfWordInfo> { sorted[i] };
                    groups.Add(current);
                }
            }

            foreach (var group in groups)
            {
                redactions.Add(new PdfRedactionInfo
                {
                    PageNumber = pageNumber,
                    OriginalText = value,
                    ReplacementText = replacement,
                    X = group.Min(w => w.X),
                    Y = group.Min(w => w.Y),
                    Width = group.Max(w => w.X + w.Width) - group.Min(w => w.X),
                    Height = group.Max(w => w.Y + w.Height) - group.Min(w => w.Y)
                });
            }

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
        /// Extrae todas las palabras del PDF agrupadas por página,
        /// ordenadas en orden de lectura natural (de arriba a abajo, izquierda a derecha).
        /// </summary>
        private Dictionary<int, List<PdfWordInfo>> ExtractAllWordsByPage(byte[] fileBytes)
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
                    .OrderByDescending(w => Math.Round(w.Y / 5.0) * 5.0)
                    .ThenBy(w => w.X)
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Extrae líneas de texto del PDF agrupando palabras por posición vertical.
        /// Usa tolerancia de 2 puntos para agrupar texto normal con negritas.
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
                    var wordsInLine = group.OrderBy(w => w.BoundingBox.Left).ToList();

                    lines.Add(new PdfLineInfo
                    {
                        PageNumber = page.Number,
                        Text = string.Join(" ", wordsInLine.Select(w => w.Text)),
                        Words = wordsInLine.Select(w => new PdfWordInfo
                        {
                            PageNumber = page.Number,
                            Text = w.Text,
                            X = w.BoundingBox.Left,
                            Y = w.BoundingBox.Bottom,
                            Width = w.BoundingBox.Width,
                            Height = w.BoundingBox.Height
                        }).ToList()
                    });
                }
            }

            return lines;
        }

        /// <summary>
        /// Busca las palabras que forman una frase usando ventana deslizante elástica.
        /// Normaliza el texto eliminando guiones y espacios para mayor tolerancia.
        /// </summary>
        private List<PdfWordInfo> FindWordsForPhrase(List<PdfWordInfo> words, string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return new List<PdfWordInfo>();

            string Normalize(string text) =>
                text.Replace(" ", "")
                    .Replace("-", "")
                    .Replace("\u2013", "")
                    .Replace("\u2014", "")
                    .ToLowerInvariant();

            var normalizedPhrase = Normalize(phrase).TrimEnd(',', '.', ';', ':');

            // Búsqueda elástica — acepta frases divididas entre palabras contiguas
            for (int start = 0; start < words.Count; start++)
            {
                var currentMatch = new List<PdfWordInfo>();
                var currentText = new StringBuilder();

                for (int end = start; end < words.Count; end++)
                {
                    currentMatch.Add(words[end]);
                    currentText.Append(words[end].Text);

                    var normalized = Normalize(currentText.ToString())
                        .TrimEnd(',', '.', ';', ':');

                    if (normalized == normalizedPhrase)
                        return currentMatch;

                    if (normalized.Length > normalizedPhrase.Length + 5)
                        break;
                }
            }

            // Fallback: búsqueda exacta para palabras individuales
            var phraseWords = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (phraseWords.Length == 1)
            {
                var singleNormalized = Normalize(phraseWords[0]);

                var single = words.FirstOrDefault(w =>
                    Normalize(w.Text).Trim(',', '.', ';', ':', '(', ')', '"', '\'')
                    == singleNormalized);

                if (single != null)
                    return new List<PdfWordInfo> { single };
            }

            return new List<PdfWordInfo>();
        }

        /// <summary>
        /// Extrae los campos AcroForm de texto con valor y los convierte en PdfWordInfo
        /// usando las coordenadas del campo como bounding box.
        /// Esto permite que BuildRedactions los encuentre igual que texto estático.
        ///
        /// Se agrupa por página para integrarse directamente con allWordsByPage y lines.
        /// </summary>
        private static Dictionary<int, List<PdfWordInfo>> ExtractAcroFormWords(byte[] fileBytes)
        {
            var result = new Dictionary<int, List<PdfWordInfo>>();

            try
            {
                using var stream = new MemoryStream(fileBytes);
                using var pdf = PdfPigDocument.Open(stream);

                if (!pdf.TryGetForm(out var form) || form?.Fields == null)
                    return result;

                for (int pageNum = 1; pageNum <= pdf.NumberOfPages; pageNum++)
                {
                    var fieldsOnPage = form.GetFieldsForPage(pageNum);

                    foreach (var field in fieldsOnPage)
                    {
                        try
                        {
                            if (field is not AcroTextField textField) continue;

                            var value = textField.Value ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(value)) continue;

                            if (!textField.Bounds.HasValue) continue;

                            var bounds = textField.Bounds.Value;

                            if (!result.ContainsKey(pageNum))
                                result[pageNum] = new List<PdfWordInfo>();

                            // Cada campo se convierte en un PdfWordInfo independiente.
                            // Si el valor tiene espacios (ej: "Brandon José"), se agrega
                            // también como palabras individuales para que el Nivel 3 las encuentre.
                            result[pageNum].Add(new PdfWordInfo
                            {
                                PageNumber = pageNum,
                                Text = value,
                                X = bounds.Left,
                                Y = bounds.Bottom,
                                Width = bounds.Width,
                                Height = bounds.Height
                            });

                            // Palabras individuales para nombres compuestos en campos separados
                            if (value.Contains(' '))
                            {
                                var parts = value.Split(' ',
                                    StringSplitOptions.RemoveEmptyEntries);

                                // Distribuir el ancho del campo entre las palabras
                                var partWidth = bounds.Width / parts.Length;

                                for (int i = 0; i < parts.Length; i++)
                                {
                                    result[pageNum].Add(new PdfWordInfo
                                    {
                                        PageNumber = pageNum,
                                        Text = parts[i],
                                        X = bounds.Left + (i * partWidth),
                                        Y = bounds.Bottom,
                                        Width = partWidth,
                                        Height = bounds.Height
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Campo individual no procesable — continuar
                        }
                    }
                }
            }
            catch
            {
                // PDF sin AcroForm o no procesable — retornar vacío
            }

            return result;
        }
    }
}