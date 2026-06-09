using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs.Analysis;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using UglyToad.PdfPig;

namespace Anonimizador___API.Application.Services.Analysis
{
    /// <summary>
    /// Servicio híbrido de análisis de documentos.
    /// Combina detección precisa mediante Regex con detección semántica mediante IA.
    ///
    /// Motor de IA activo: Ollama/Mistral (local).
    /// Para cambiar a Gemini (nube): ver comentarios en el constructor y ApplyAiDetectionAsync.
    /// Ambos motores exponen el mismo método GenerateAsync() — el cambio es transparente.
    /// </summary>
    public class DocumentAnalysisService : IDocumentAnalysisService
    {
        // =============================================
        // MOTOR DE IA — CAMBIAR SEGÚN EL ENTORNO
        // Para usar Ollama (local): descomentar OllamaService
        // Para usar Gemini (nube):  descomentar GeminiService
        // =============================================

        // private readonly GeminiService _gemini;
        private readonly OllamaService _ollama;
        private readonly ILogger<DocumentAnalysisService> _logger;

        /// <summary>
        /// Inicializa el servicio con sus dependencias.
        /// </summary>
        public DocumentAnalysisService(
            // GeminiService gemini,      
            OllamaService ollama,
            ILogger<DocumentAnalysisService> logger)
        {
            // _gemini = gemini;  
            _ollama = ollama;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DocumentAnalysisResultDto> AnalyzeAsync(
            IFormFile file,
            string? additionalContext = null)
        {
            _logger.LogInformation("Iniciando análisis híbrido de documento");

            var text = await ExtractTextAsync(file);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException(
                    "No se pudo extraer texto del documento. " +
                    "El archivo puede estar vacío, corrupto o ser una imagen sin OCR.");

            var regexResult = ApplyRegexDetection(text);
            var aiResult = await ApplyAiDetectionAsync(text, additionalContext);
            var result = MergeResults(regexResult, aiResult);

            result.PreviewText = text.Length > 3000
                ? text[..3000] + "..."
                : text;

            _logger.LogInformation(
                "Análisis completado. Personas detectadas: {Count}",
                result.DetectedPersons.Count);

            return result;
        }

        /// <summary>
        /// Extrae el texto plano de un archivo .docx o .pdf.
        /// </summary>
        private async Task<string> ExtractTextAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var bytes = stream.ToArray();

            return extension switch
            {
                ".docx" => ExtractTextFromDocx(bytes),
                ".pdf" => ExtractTextFromPdf(bytes),
                _ => throw new ArgumentException(
                               $"Formato no soportado para análisis: {extension}. " +
                               "Solo se aceptan .docx y .pdf.")
            };
        }

        /// <summary>
        /// Extrae texto de un documento Word (.docx).
        /// </summary>
        private string ExtractTextFromDocx(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);

            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            var sb = new StringBuilder();

            foreach (var text in body.Descendants<Text>())
                sb.Append(text.Text + " ");

            return sb.ToString();
        }

        /// <summary>
        /// Extrae texto de un PDF agrupando palabras por línea.
        /// También extrae los valores de campos de formulario AcroForm (PDFs rellenables)
        /// para cubrir formularios institucionales como los del INS o la CCSS.
        /// </summary>
        private string ExtractTextFromPdf(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var pdf = PdfDocument.Open(ms);

            var sb = new StringBuilder();

            // ── Texto estático — párrafos, tablas, encabezados ───────────────
            foreach (var page in pdf.GetPages())
            {
                var lineGroups = page.GetWords()
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 2) * 2)
                    .OrderByDescending(g => g.Key);

                foreach (var group in lineGroups)
                {
                    var lineText = string.Join(" ",
                        group.OrderBy(w => w.BoundingBox.Left)
                             .Select(w => w.Text));

                    sb.AppendLine(lineText);
                }
            }

            // ── Campos de formulario AcroForm (PDFs rellenables) ─────────────
            // Cubre formularios institucionales donde los datos se ingresan en
            // campos interactivos y no quedan como texto plano extraíble con GetWords().
            try
            {
                if (pdf.TryGetForm(out var form) && form?.Fields != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("=== CAMPOS DEL FORMULARIO ===");

                    foreach (var field in form.Fields)
                    {
                        try
                        {
                            // Nombre del campo (T)
                            var fieldName = string.Empty;
                            if (field.Dictionary.TryGet(
                                    UglyToad.PdfPig.Tokens.NameToken.T,
                                    out UglyToad.PdfPig.Tokens.IToken tToken))
                                fieldName = tToken.ToString()?.Trim('(', ')') ?? string.Empty;

                            // Valor del campo (V)
                            var fieldValue = string.Empty;
                            if (field.Dictionary.TryGet(
                                    UglyToad.PdfPig.Tokens.NameToken.V,
                                    out UglyToad.PdfPig.Tokens.IToken vToken))
                                fieldValue = vToken.ToString()?.Trim('(', ')') ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(fieldValue))
                                sb.AppendLine($"{fieldName}: {fieldValue}");
                        }
                        catch
                        {
                            // Campo individual no parseable — continuar con el siguiente
                        }
                    }
                }
            }
            catch
            {
                // El PDF no tiene AcroForm o no es parseable — no afecta el texto estático
            }

            return sb.ToString();
        }

        /// <summary>
        /// Aplica detección rápida con expresiones regulares del <see cref="RegexCatalog"/>.
        /// Solo detecta la primera coincidencia de cada tipo.
        /// </summary>
        private DocumentAnalysisResultDto ApplyRegexDetection(string text)
        {
            var result = new DocumentAnalysisResultDto();
            var person = new DetectedPersonDto
            {
                Email = RegexCatalog.Email.Match(text).Value.NullIfEmpty(),
                Identification = RegexCatalog.CostaRicaId.Match(text).Value.NullIfEmpty(),
                PhoneNumber = RegexCatalog.Phone.Match(text).Value.NullIfEmpty(),
                FullName = RegexCatalog.FullName.Match(text).Value.NullIfEmpty()
            };

            if (person.Email != null ||
                person.Identification != null ||
                person.PhoneNumber != null ||
                person.FullName != null)
            {
                result.DetectedPersons.Add(person);
            }

            return result;
        }

        /// <summary>
        /// Aplica detección semántica usando el modelo Ollama configurado.
        /// Usa formato estructurado línea por línea para mayor robustez con Mistral.
        /// </summary>
        private async Task<DocumentAnalysisResultDto> ApplyAiDetectionAsync(
            string text,
            string? additionalContext)
        {
            var contextClause = string.IsNullOrWhiteSpace(additionalContext)
                ? string.Empty
                : $"\nContexto adicional: {additionalContext}";

            var truncatedText = text.Length > 4000
                ? text[..4000]
                : text;

            var prompt = $"""
                Eres un asistente experto en anonimización de documentos legales costarricenses.
                Analiza el texto y extrae los datos de CADA persona mencionada.

                INSTRUCCIONES PARA VARIACIONES:
                Después de identificar el nombre completo de cada persona, revisa el texto
                completo y busca TODAS las formas abreviadas con que se le menciona:
                - Solo primer apellido
                - Solo segundo apellido
                - Ambos apellidos sin nombre
                - Solo nombre de pila
                - Combinación de nombre y primer apellido
                Incluye en VARIACIONES únicamente los fragmentos exactos que aparecen
                en el texto, tal como están escritos, sin artículos ni tratamientos
                como "señor", "señora", "licenciado", "el", "la".

                USA EXACTAMENTE este formato para cada persona:

                ---PERSONA---
                NOMBRE: [nombre completo exacto como aparece en el texto, o NONE]
                CEDULA: [número de cédula formato X-XXXX-XXXX, o NONE]
                EMAIL: [correo electrónico, o NONE]
                TELEFONO: [número de teléfono, o NONE]
                CARGO: [cargo o puesto, o NONE]
                DIRECCION: [dirección física completa, o NONE]
                INSTITUCION: [institución u organización a la que pertenece, o NONE]
                CUENTA_BANCARIA: [número de cuenta bancaria si aparece, o NONE]
                CONDICION_MEDICA: [condición médica o diagnóstico si aparece, o NONE]
                VARIACIONES: [otras formas abreviadas del NOMBRE que aparecen en el texto, separadas por coma. NO incluyas cargos ni organizaciones. Si no hay variaciones escribe NONE]
                ---FIN---

                Para datos adicionales sensibles que no pertenecen a una persona específica:

                ---EXTRA---
                TIPO: [BANK_ACCOUNT o DISEASE o OTHER]
                VALOR: [el valor detectado]
                ---FIN---

                REGLAS ESTRICTAS:
                - Cada bloque empieza con ---PERSONA--- o ---EXTRA--- y termina con ---FIN---
                - No escribas nada fuera de los bloques
                - Si un dato no existe escribe NONE
                - Las variaciones deben ser fragmentos que realmente aparecen en el texto{contextClause}

                Texto:
                {truncatedText}
                """;

            try
            {
                //var response = await _gemini.GenerateAsync(prompt);
                var response = await _ollama.GenerateAsync(prompt);
                var result = ParseStructuredResponse(response);
                return MergePersons(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detección con IA falló — usando solo Regex");
                return new DocumentAnalysisResultDto();
            }
        }

        /// <summary>
        /// Parsea la respuesta estructurada de Ollama línea por línea.
        /// Más robusto que parsear JSON libre con modelos como Mistral.
        /// </summary>
        private DocumentAnalysisResultDto ParseStructuredResponse(string response)
        {
            var result = new DocumentAnalysisResultDto();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var inPersona = false;
            var inExtra = false;
            DetectedPersonDto? currentPerson = null;
            string? extraType = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line == "---PERSONA---")
                {
                    currentPerson = new DetectedPersonDto();
                    inPersona = true;
                    inExtra = false;
                    extraType = null;
                    continue;
                }

                if (line == "---EXTRA---")
                {
                    inExtra = true;
                    inPersona = false;
                    extraType = null;
                    continue;
                }

                if (line == "---FIN---")
                {
                    if (inPersona && currentPerson != null && currentPerson.HasAnyField())
                        result.DetectedPersons.Add(currentPerson);

                    currentPerson = null;
                    inPersona = false;
                    inExtra = false;
                    continue;
                }

                var (key, value) = SplitKeyValue(line);
                if (value == null || value == "NONE") continue;

                if (inPersona && currentPerson != null)
                {
                    switch (key)
                    {
                        case "NOMBRE": currentPerson.FullName = value; break;
                        case "CEDULA": currentPerson.Identification = value; break;
                        case "EMAIL": currentPerson.Email = value; break;
                        case "TELEFONO": currentPerson.PhoneNumber = value; break;
                        case "CARGO": currentPerson.Position = value; break;
                        case "DIRECCION": currentPerson.Address = value; break;
                        case "INSTITUCION": currentPerson.Institution = value; break;  // ← nuevo
                        case "CUENTA_BANCARIA": currentPerson.BankAccount = value; break;  // ← nuevo
                        case "CONDICION_MEDICA": currentPerson.MedicalCondition = value; break; // ← nuevo
                        case "VARIACIONES":
                            currentPerson.NameVariations = value
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => v.Trim())
                                .Where(v => v.Length > 2 &&
                                            v != "NONE" &&
                                            !v.Contains("S.A.", StringComparison.OrdinalIgnoreCase) &&
                                            !v.Contains("S.R.L.", StringComparison.OrdinalIgnoreCase) &&
                                            !v.Contains("Ltda", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            break;
                    }
                }

                if (inExtra && key == "TIPO")
                    extraType = value;
                else if (inExtra && key == "VALOR" && extraType != null)
                {
                    result.AdditionalData.Add(new AdditionalDataDto
                    {
                        Type = extraType,
                        Value = value
                    });
                    extraType = null;
                }
            }

            return result;
        }

        /// <summary>
        /// Fusiona personas que son variaciones de una ya detectada,
        /// evitando duplicados en el resultado final.
        /// </summary>
        private DocumentAnalysisResultDto MergePersons(DocumentAnalysisResultDto result)
        {
            if (result.DetectedPersons.Count <= 1)
                return result;

            var merged = new List<DetectedPersonDto>();
            var handled = new HashSet<int>();

            for (int i = 0; i < result.DetectedPersons.Count; i++)
            {
                if (handled.Contains(i)) continue;

                var primary = result.DetectedPersons[i];

                if (string.IsNullOrWhiteSpace(primary.FullName))
                {
                    handled.Add(i);
                    continue;
                }

                for (int j = i + 1; j < result.DetectedPersons.Count; j++)
                {
                    if (handled.Contains(j)) continue;

                    var candidate = result.DetectedPersons[j];

                    if (!IsVariationOf(candidate, primary)) continue;

                    if (!string.IsNullOrWhiteSpace(candidate.FullName) &&
                        !primary.NameVariations.Contains(
                            candidate.FullName, StringComparer.OrdinalIgnoreCase))
                    {
                        primary.NameVariations.Add(candidate.FullName);
                    }

                    foreach (var v in candidate.NameVariations)
                    {
                        if (!primary.NameVariations.Contains(
                            v, StringComparer.OrdinalIgnoreCase))
                            primary.NameVariations.Add(v);
                    }

                    primary.Identification ??= candidate.Identification;
                    primary.Email ??= candidate.Email;
                    primary.PhoneNumber ??= candidate.PhoneNumber;
                    primary.Position ??= candidate.Position;
                    primary.Address ??= candidate.Address;

                    handled.Add(j);
                }

                merged.Add(primary);
                handled.Add(i);
            }

            result.DetectedPersons = merged;
            return result;
        }

        /// <summary>
        /// Determina si un candidato es una variación del nombre de la persona primaria.
        /// </summary>
        private bool IsVariationOf(DetectedPersonDto candidate, DetectedPersonDto primary)
        {
            if (string.IsNullOrWhiteSpace(candidate.FullName) ||
                string.IsNullOrWhiteSpace(primary.FullName))
                return false;

            var primaryName = primary.FullName.ToLowerInvariant();
            var candidateName = candidate.FullName.ToLowerInvariant();

            if (primaryName.Contains(candidateName) ||
                candidateName.Contains(primaryName))
                return true;

            var primaryWords = primaryName.Split(' ').Where(w => w.Length > 3).ToHashSet();
            var candidateWords = candidateName.Split(' ').Where(w => w.Length > 3).ToList();

            return candidateWords.Count(w => primaryWords.Contains(w)) >= 2;
        }

        /// <summary>
        /// Combina los resultados de Regex e IA complementando campos faltantes.
        /// </summary>
        private DocumentAnalysisResultDto MergeResults(
            DocumentAnalysisResultDto regex,
            DocumentAnalysisResultDto ai)
        {
            var merged = new DocumentAnalysisResultDto();
            merged.DetectedPersons.AddRange(ai.DetectedPersons);
            merged.AdditionalData.AddRange(ai.AdditionalData);

            foreach (var regexPerson in regex.DetectedPersons)
            {
                var existing = merged.DetectedPersons.FirstOrDefault();

                if (existing == null)
                {
                    merged.DetectedPersons.Add(regexPerson);
                    continue;
                }

                existing.Email ??= regexPerson.Email;
                existing.Identification ??= regexPerson.Identification;
                existing.PhoneNumber ??= regexPerson.PhoneNumber;
                existing.FullName ??= regexPerson.FullName;
            }

            return MergePersons(merged);
        }

        /// <summary>
        /// Divide una línea con formato "CLAVE: valor" en sus dos componentes.
        /// </summary>
        private (string Key, string? Value) SplitKeyValue(string line)
        {
            var idx = line.IndexOf(':');
            if (idx < 0) return (line, null);

            var key = line[..idx].Trim().ToUpperInvariant();
            var value = line[(idx + 1)..].Trim();

            return (key, string.IsNullOrWhiteSpace(value) ? null : value);
        }
    }

    /// <summary>
    /// Extensiones de utilidad para strings en el contexto del análisis.
    /// </summary>
    internal static class StringExtensions
    {
        /// <summary>
        /// Retorna null si el string está vacío o es whitespace.
        /// </summary>
        public static string? NullIfEmpty(this string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Extensiones de utilidad para <see cref="DetectedPersonDto"/>.
    /// </summary>
    internal static class DetectedPersonExtensions
    {
        /// <summary>
        /// Indica si la persona tiene al menos un campo con valor.
        /// </summary>
        public static bool HasAnyField(this DetectedPersonDto person) =>
            person.FullName != null ||
            person.Identification != null ||
            person.Email != null ||
            person.PhoneNumber != null ||
            person.Position != null ||
            person.Address != null;
    }
}