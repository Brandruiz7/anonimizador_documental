using Anonimizador___API.Application.Common;
using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Anonimizador___API.Application.Services
{
    /// <summary>
    /// Servicio híbrido de análisis de documentos.
    /// Combina Regex para detección precisa e IA para detección semántica.
    /// </summary>
    public class DocumentAnalysisService : IDocumentAnalysisService
    {
        private readonly OllamaService _ollama;
        private readonly ILogger<DocumentAnalysisService> _logger;

        public DocumentAnalysisService(
            OllamaService ollama,
            ILogger<DocumentAnalysisService> logger)
        {
            _ollama = ollama;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DocumentAnalysisResultDto> AnalyzeAsync(
            IFormFile file,
            string? additionalContext = null)
        {
            _logger.LogInformation(
                "Starting hybrid document analysis");

            // 1. Extraer texto del documento
            var text = await ExtractTextAsync(file);

            if (string.IsNullOrWhiteSpace(text))
                throw new Exception(
                    "No se pudo extraer texto del documento.");

            // 2. Detección con Regex (rápida y precisa)
            var regexDetected = ApplyRegexDetection(text);

            // 3. Detección con IA (semántica)
            var aiDetected = await ApplyAiDetectionAsync(
                text, additionalContext);

            // 4. Combinar resultados
            var result = MergeResults(regexDetected, aiDetected);

            // Incluir texto para vista previa (primeros 3000 chars)
            result.PreviewText = text.Length > 3000
                ? text[..3000] + "..."
                : text;

            _logger.LogInformation(
                "Analysis complete. Detected {Count} person(s)",
                result.DetectedPersons.Count);

            return result;
        }

        /// <summary>
        /// Extrae texto plano del documento.
        /// Soporta DOCX y PDF.
        /// </summary>
        private async Task<string> ExtractTextAsync(IFormFile file)
        {
            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var bytes = stream.ToArray();

            if (extension == ".docx")
                return ExtractTextFromDocx(bytes);

            if (extension == ".pdf")
                return ExtractTextFromPdf(bytes);

            throw new Exception(
                $"Formato no soportado: {extension}");
        }

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

        private string ExtractTextFromPdf(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var pdf = PdfDocument.Open(ms);

            var sb = new StringBuilder();

            foreach (var page in pdf.GetPages())
            {
                // Agrupar palabras por línea con tolerancia
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

            return sb.ToString();
        }

        /// <summary>
        /// Detección rápida con Regex usando RegexCatalog.
        /// </summary>
        private DocumentAnalysisResultDto ApplyRegexDetection(string text)
        {
            var result = new DocumentAnalysisResultDto();
            var person = new DetectedPersonDto();

            // Email
            var emailMatch = RegexCatalog.Email.Match(text);
            if (emailMatch.Success)
                person.Email = emailMatch.Value;

            // Cédula costarricense
            var idMatch = RegexCatalog.CostaRicaId.Match(text);
            if (idMatch.Success)
                person.Identification = idMatch.Value;

            // Teléfono
            var phoneMatch = RegexCatalog.Phone.Match(text);
            if (phoneMatch.Success)
                person.PhoneNumber = phoneMatch.Value;

            // Nombre
            var nameMatch = RegexCatalog.FullName.Match(text);
            if (nameMatch.Success)
                person.FullName = nameMatch.Value;

            // Si detectó algo, agrega la persona
            if (person.Email != null ||
                person.Identification != null ||
                person.PhoneNumber != null ||
                person.FullName != null)
            {
                result.DetectedPersons.Add(person);
            }

            return result;
        }

        private async Task<DocumentAnalysisResultDto> ApplyAiDetectionAsync(string text, string? additionalContext)
        {
            var contextClause = string.IsNullOrWhiteSpace(additionalContext)
                ? ""
                : $"\nContexto adicional: {additionalContext}";

            var truncatedText = text.Length > 4000
                ? text[..4000]
                : text;

            // Prompt con formato línea por línea — más fácil de parsear
            // que JSON libre para modelos como Mistral
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
                VARIACIONES: [otras formas abreviadas del NOMBRE de la persona que aparecen en el texto, separadas por coma. NO incluyas cargos, empresas ni organizaciones. Solo fragmentos del nombre completo. Si no hay variaciones escribe NONE]
                ---FIN---

                Para datos adicionales sensibles (cuentas bancarias, enfermedades, etc.):

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
                var response = await _ollama.GenerateAsync(prompt);

                _logger.LogWarning("Ollama raw response: {Response}", response);

                var result = ParseStructuredResponse(response);

                return MergePersons(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI detection failed");
                return new DocumentAnalysisResultDto();
            }
        }

        /// <summary>
        /// Parsea la respuesta estructurada línea por línea.
        /// Mucho más robusto que parsear JSON libre de Mistral.
        /// </summary>
        private DocumentAnalysisResultDto ParseStructuredResponse(string response)
        {
            var result = new DocumentAnalysisResultDto();
            var lines = response.Split('\n',
                StringSplitOptions.RemoveEmptyEntries);

            DetectedPersonDto? currentPerson = null;
            bool inExtra = false;
            bool inPersona = false;
            string? extraType = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Detectar inicio de bloque
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

                // Detectar fin de bloque
                if (line == "---FIN---")
                {
                    if (inPersona && currentPerson != null)
                    {
                        if (currentPerson.FullName != null ||
                            currentPerson.Identification != null ||
                            currentPerson.Email != null ||
                            currentPerson.PhoneNumber != null ||
                            currentPerson.Position != null ||
                            currentPerson.Address != null)
                        {
                            result.DetectedPersons.Add(currentPerson);
                        }
                        currentPerson = null;
                    }

                    inPersona = false;
                    inExtra = false;
                    continue;
                }

                var (key, value) = SplitKeyValue(line);
                if (value == null || value == "NONE") continue;

                // Parsear campos de persona
                if (inPersona && currentPerson != null)
                {
                    switch (key)
                    {
                        case "NOMBRE":
                            currentPerson.FullName = value;
                            break;
                        case "CEDULA":
                            currentPerson.Identification = value;
                            break;
                        case "EMAIL":
                            currentPerson.Email = value;
                            break;
                        case "TELEFONO":
                            currentPerson.PhoneNumber = value;
                            break;
                        case "CARGO":
                            currentPerson.Position = value;
                            break;
                        case "DIRECCION":
                            currentPerson.Address = value;
                            break;
                        case "VARIACIONES":
                            currentPerson.NameVariations = value
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => v.Trim())
                                .Where(v => !string.IsNullOrWhiteSpace(v) &&
                                            v != "NONE" &&
                                            v.Length > 2 &&
                                            !v.Contains("S.A.", StringComparison.OrdinalIgnoreCase) &&
                                            !v.Contains("S.R.L.", StringComparison.OrdinalIgnoreCase) &&
                                            !v.Contains("Ltda", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            break;
                    }
                }

                // Parsear datos adicionales
                if (inExtra)
                {
                    if (key == "TIPO")
                        extraType = value;
                    else if (key == "VALOR" && extraType != null)
                    {
                        result.AdditionalData.Add(new AdditionalDataDto
                        {
                            Type = extraType,
                            Value = value
                        });
                        extraType = null;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Fusiona personas que son en realidad variaciones de una persona ya detectada.
        /// Ej: si "Mora Sandoval" ya existe como variación de "Carlos Alberto Mora Sandoval",
        /// no debe aparecer como persona separada.
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

                // Si no tiene nombre completo es probablemente una variación
                if (string.IsNullOrWhiteSpace(primary.FullName))
                {
                    handled.Add(i);
                    continue;
                }

                for (int j = i + 1; j < result.DetectedPersons.Count; j++)
                {
                    if (handled.Contains(j)) continue;

                    var candidate = result.DetectedPersons[j];

                    // Verificar si el candidato es una variación de la persona primaria
                    if (IsVariationOf(candidate, primary))
                    {
                        // Fusionar — agregar nombre del candidato como variación
                        if (!string.IsNullOrWhiteSpace(candidate.FullName) &&
                            !primary.NameVariations.Contains(candidate.FullName,
                                StringComparer.OrdinalIgnoreCase))
                        {
                            primary.NameVariations.Add(candidate.FullName);
                        }

                        // Absorber variaciones del candidato
                        foreach (var v in candidate.NameVariations)
                        {
                            if (!primary.NameVariations.Contains(v,
                                StringComparer.OrdinalIgnoreCase))
                                primary.NameVariations.Add(v);
                        }

                        // Completar datos faltantes
                        primary.Identification ??= candidate.Identification;
                        primary.Email ??= candidate.Email;
                        primary.PhoneNumber ??= candidate.PhoneNumber;
                        primary.Position ??= candidate.Position;
                        primary.Address ??= candidate.Address;

                        handled.Add(j);
                    }
                }

                merged.Add(primary);
                handled.Add(i);
            }

            result.DetectedPersons = merged;
            return result;
        }

        /// <summary>
        /// Determina si un candidato es una variación de la persona primaria.
        /// </summary>
        private bool IsVariationOf(
            DetectedPersonDto candidate,
            DetectedPersonDto primary)
        {
            if (string.IsNullOrWhiteSpace(candidate.FullName) ||
                string.IsNullOrWhiteSpace(primary.FullName))
                return false;

            var primaryName = primary.FullName.ToLowerInvariant();
            var candidateName = candidate.FullName.ToLowerInvariant();

            // El candidato es substring del nombre primario
            if (primaryName.Contains(candidateName))
                return true;

            // El nombre primario es substring del candidato
            if (candidateName.Contains(primaryName))
                return true;

            // Comparten al menos dos palabras significativas
            var primaryWords = primaryName.Split(' ')
                .Where(w => w.Length > 3).ToHashSet();
            var candidateWords = candidateName.Split(' ')
                .Where(w => w.Length > 3).ToList();

            var sharedWords = candidateWords
                .Count(w => primaryWords.Contains(w));

            return sharedWords >= 2;
        }

        /// <summary>
        /// Divide una línea "CLAVE: valor" en clave y valor.
        /// </summary>
        private (string Key, string? Value) SplitKeyValue(string line)
        {
            var idx = line.IndexOf(':');
            if (idx < 0) return (line, null);

            var key = line[..idx].Trim().ToUpperInvariant();
            var value = line[(idx + 1)..].Trim();

            return (key, string.IsNullOrWhiteSpace(value) ? null : value);
        }

        /// <summary>
        /// Combina los resultados de Regex e IA evitando duplicados.
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

            // Fusionar personas duplicadas del resultado final
            return MergePersons(merged);
        }

        private string? GetNullableString(
            JsonElement element,
            string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
                return null;

            // Si es array, tomar el primer elemento válido
            if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val) &&
                            val != "n/a" && val != "null")
                            return val;
                    }
                }
                return null;
            }

            if (prop.ValueKind == JsonValueKind.Null)
                return null;

            var value = prop.GetString();

            return string.IsNullOrWhiteSpace(value) ||
                   value == "n/a" || value == "null"
                ? null
                : value;
        }
    }
}