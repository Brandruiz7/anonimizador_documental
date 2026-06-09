using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;
using Anonimizador___API.Interfaces.Repositories;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;
using PDFtoImage;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SkiaSharp;
using UglyToad.PdfPig.AcroForms.Fields;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace Anonimizador___API.Application.Services.Documents
{
    /// <summary>
    /// Servicio principal de documentos.
    /// Orquesta la validación, el procesamiento y la auditoría del flujo de anonimización.
    ///
    /// Flujo completo:
    /// 1. Valida el archivo (extensión, tipo MIME, tamaño, estructura interna)
    /// 2. Calcula el hash SHA256 del original para trazabilidad
    /// 3. Registra el proceso en BD con estado PROCESSING
    /// 4. Construye los targets de anonimización desde el request
    /// 5. Delega el procesamiento al procesador correspondiente (DOCX o PDF)
    /// 6. Registra la versión anonimizada y la auditoría campo por campo
    /// 7. Actualiza el estado a ANONYMIZED y retorna el stream del resultado
    /// Si ocurre cualquier error después del paso 3, el estado se actualiza a FAILED.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        // Estados del proceso — deben coincidir con la tabla PROCESS_STATUS en BD
        private const int StatusProcessing = 2;
        private const int StatusAnonymized = 3;
        private const int StatusFailed = 4;

        // Límite de tamaño: 100 MB en bytes
        private const long MaxFileSizeBytes = 104857600;

        private static readonly string[] AllowedExtensions =
            { ".docx", ".pdf" };

        private static readonly string[] AllowedContentTypes =
        {
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/pdf"
        };

        private readonly IDocumentRepository _repository;
        private readonly IEnumerable<IDocumentProcessor> _processors;
        private readonly ILogger<DocumentService> _logger;

        /// <summary>
        /// Inicializa el servicio con sus dependencias.
        /// </summary>
        public DocumentService(
            IDocumentRepository repository,
            IEnumerable<IDocumentProcessor> processors,
            ILogger<DocumentService> logger)
        {
            _repository = repository;
            _processors = processors;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<(Stream FileStream, string FileName, string ContentType)> UploadStreamAsync(
            UploadDocumentRequestDto request)
        {
            // processId fuera del try para poder marcarlo como FAILED en el catch
            var processId = 0;

            try
            {
                _logger.LogInformation("Iniciando proceso de anonimización");

                // 1. Validaciones básicas del archivo
                var file = request.File
                    ?? throw new ArgumentNullException(nameof(request.File), "El archivo es nulo.");

                if (file.Length == 0)
                    throw new ArgumentException("El archivo está vacío.");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!AllowedExtensions.Contains(extension))
                    throw new ArgumentException(
                        $"Extensión no permitida: {extension}. Solo se aceptan .docx y .pdf.");

                if (!AllowedContentTypes.Contains(file.ContentType))
                    throw new ArgumentException(
                        $"Tipo de contenido no permitido: {file.ContentType}.");

                if (file.Length > MaxFileSizeBytes)
                    throw new ArgumentException(
                        $"El archivo supera el tamaño máximo permitido (100 MB).");

                // 2. Cargar archivo en memoria
                var workStream = new MemoryStream();
                await file.OpenReadStream().CopyToAsync(workStream);
                workStream.Position = 0;

                _logger.LogInformation("Archivo cargado en memoria");

                // 3. Validar estructura interna del documento
                // Detecta archivos corruptos o con extensión incorrecta antes de procesarlos
                try
                {
                    if (extension == ".docx")
                    {
                        using var doc = WordprocessingDocument.Open(workStream, false);
                    }
                    else
                    {
                        using var reader = new StreamReader(workStream, leaveOpen: true);
                        var header = new char[5];
                        await reader.ReadBlockAsync(header, 0, 5);

                        if (!new string(header).StartsWith("%PDF"))
                            throw new ArgumentException("El archivo PDF no es válido.");
                    }

                    workStream.Position = 0;
                }
                catch
                {
                    await workStream.DisposeAsync();
                    throw new ArgumentException(
                        $"Estructura de documento inválida para extensión {extension}.");
                }

                // 4. Calcular hash SHA256 del archivo original
                // Se usa para detectar duplicados y garantizar integridad del original
                string originalHash;
                using (var sha256 = SHA256.Create())
                {
                    workStream.Position = 0;
                    var hashBytes = await sha256.ComputeHashAsync(workStream);
                    originalHash = BitConverter.ToString(hashBytes).Replace("-", "");
                    workStream.Position = 0;
                }

                // 5. Registrar proceso en BD — estado inicial: PROCESSING
                processId = await _repository.InsertDocumentProcessAsync(
                    file.FileName,
                    file.ContentType,
                    file.Length / 1024,
                    originalHash,
                    request.UploadedBy);

                // 6. Construir targets de anonimización
                if (request.Persons == null || request.Persons.Count == 0)
                    throw new ArgumentException("Debe proporcionar al menos una persona.");

                var targets = BuildAnonymizationTargets(request);

                if (targets.Count == 0)
                    throw new ArgumentException(
                        "No se proporcionaron datos válidos para anonimizar.");

                // 7. Seleccionar procesador según extensión y anonimizar en memoria
                // El documento nunca se escribe a disco — todo ocurre en RAM
                workStream.Position = 0;
                var fileBytes = workStream.ToArray();
                await workStream.DisposeAsync();

                var processor = _processors.FirstOrDefault(p => p.CanProcess(extension))
                    ?? throw new InvalidOperationException(
                        $"No hay procesador disponible para {extension}.");

                // Si el PDF tiene campos AcroForm (formulario rellenable),
                // aplanarlo antes de procesar para que PdfPig pueda leer
                // los valores como texto estático durante la redacción.
                if (extension == ".pdf")
                    fileBytes = FlattenAcroFormIfNeeded(fileBytes);

                var result = await processor.ProcessAsync(fileBytes, targets);

                // Limpiar bytes originales de memoria por seguridad
                Array.Clear(fileBytes, 0, fileBytes.Length);

                // 8. Calcular hash del resultado anonimizado y registrar versión
                string anonymizedHash;
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(result.FileBytes);
                    anonymizedHash = BitConverter.ToString(hashBytes).Replace("-", "");
                }

                var versionId = await _repository.InsertDocumentVersionAsync(
                    processId, "ANONYMIZED", anonymizedHash);

                // 9. Registrar auditoría campo por campo
                foreach (var field in result.AuditFields)
                {
                    await _repository.InsertAuditFieldAsync(
                        versionId,
                        field.FieldType,
                        field.OriginalValue,
                        field.AnonymizedValue);
                }

                // 10. Actualizar estado a ANONYMIZED
                await _repository.UpdateProcessStatusAsync(processId, StatusAnonymized);

                _logger.LogInformation(
                    "Anonimización completada. Campos reemplazados: {Count}",
                    result.AuditFields.Count);

                // 11. Retornar stream del resultado para descarga directa
                var resultStream = new MemoryStream(result.FileBytes);
                resultStream.Position = 0;

                return (resultStream, $"ANONYMIZED_{file.FileName}", file.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la anonimización");

                // Si el proceso ya fue registrado en BD, marcarlo como FAILED
                if (processId > 0)
                {
                    try
                    {
                        await _repository.UpdateProcessStatusAsync(processId, StatusFailed);
                        _logger.LogInformation(
                            "Proceso {ProcessId} marcado como FAILED", processId);
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogError(dbEx,
                            "Error al marcar proceso {ProcessId} como FAILED", processId);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Construye la lista de targets de anonimización desde el request.
        /// Incluye datos generales del documento y todos los campos de cada persona,
        /// junto con sus variaciones de nombre, cédula y teléfono.
        /// Las personas sin ningún campo con valor se omiten silenciosamente.
        /// </summary>
        /// <param name="request">Request con personas y datos generales a anonimizar.</param>
        /// <returns>Lista de targets lista para pasar al procesador.</returns>
        private static List<AnonymizationTargetDto> BuildAnonymizationTargets(
            UploadDocumentRequestDto request)
        {
            var targets = new List<AnonymizationTargetDto>();

            // Datos generales del documento — se identifican con PersonIndex = -1
            if (!string.IsNullOrWhiteSpace(request.GeneralData?.CaseNumber) ||
                !string.IsNullOrWhiteSpace(request.GeneralData?.OfficeNumber))
            {
                targets.Add(new AnonymizationTargetDto
                {
                    PersonIndex = -1,
                    CaseNumber = request.GeneralData?.CaseNumber,
                    OfficeNumber = request.GeneralData?.OfficeNumber
                });
            }

            // Personas — se omiten las que no tienen ningún campo con valor
            for (int i = 0; i < request.Persons.Count; i++)
            {
                var p = request.Persons[i];

                var hasAnyField =
                    !string.IsNullOrWhiteSpace(p.FullName) ||
                    !string.IsNullOrWhiteSpace(p.Identification) ||
                    !string.IsNullOrWhiteSpace(p.Email) ||
                    !string.IsNullOrWhiteSpace(p.PhoneNumber) ||
                    !string.IsNullOrWhiteSpace(p.Position) ||
                    !string.IsNullOrWhiteSpace(p.Address) ||
                    !string.IsNullOrWhiteSpace(p.Institution) ||
                    !string.IsNullOrWhiteSpace(p.BankAccount) ||
                    !string.IsNullOrWhiteSpace(p.MedicalCondition) ||
                    !string.IsNullOrWhiteSpace(p.FreeText);

                if (!hasAnyField) continue;

                targets.Add(new AnonymizationTargetDto
                {
                    PersonIndex = i,
                    FullName = p.FullName,
                    Identification = p.Identification,
                    Email = p.Email,
                    PhoneNumber = p.PhoneNumber,
                    Position = p.Position,
                    Address = p.Address,
                    Institution = p.Institution,
                    BankAccount = p.BankAccount,
                    MedicalCondition = p.MedicalCondition,
                    FreeText = p.FreeText
                });

                // Variaciones de nombre — misma etiqueta que el nombre principal
                foreach (var variation in p.NameVariations.Where(v =>
                    !string.IsNullOrWhiteSpace(v)))
                {
                    targets.Add(new AnonymizationTargetDto
                    {
                        PersonIndex = i,
                        FullName = variation
                    });
                }

                // Variaciones de cédula — misma etiqueta que la cédula principal
                foreach (var variation in p.IdVariations.Where(v =>
                    !string.IsNullOrWhiteSpace(v)))
                {
                    targets.Add(new AnonymizationTargetDto
                    {
                        PersonIndex = i,
                        Identification = variation
                    });
                }

                // Variaciones de teléfono — misma etiqueta que el teléfono principal
                foreach (var variation in p.PhoneVariations.Where(v =>
                    !string.IsNullOrWhiteSpace(v)))
                {
                    targets.Add(new AnonymizationTargetDto
                    {
                        PersonIndex = i,
                        PhoneNumber = variation
                    });
                }
            }

            return targets;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync()
            => await _repository.GetAllDocumentsAsync();

        /// <inheritdoc />
        public async Task<MetricsResponseDto> GetMetricsAsync()
            => await _repository.GetMetricsAsync();


        /// <summary>
        /// Anteriormente aplanaba PDFs con AcroForm antes de procesarlos.
        /// Ya no es necesario — PdfDocumentProcessor extrae los campos AcroForm
        /// directamente mediante ExtractAcroFormWords() en BuildRedactions.
        /// Se mantiene el método para no romper la firma, pero retorna los bytes sin cambios.
        /// </summary>
        private byte[] FlattenAcroFormIfNeeded(byte[] fileBytes) => fileBytes;

        /// <summary>
        /// Extrae los campos AcroForm de texto con valor, página y coordenadas.
        /// Usa GetFieldsForPage para asociar cada campo a su página correctamente.
        /// Solo procesa AcroTextField — los checkboxes y combos no contienen
        /// datos personales relevantes para anonimización.
        /// </summary>
        private static List<AcroFieldInfo> ExtractAcroFieldValues(byte[] fileBytes)
        {
            var result = new List<AcroFieldInfo>();

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

                        result.Add(new AcroFieldInfo
                        {
                            Value = value,
                            PageNumber = pageNum,
                            X = bounds.Left,
                            Y = bounds.Bottom,
                            FieldWidth = bounds.Width,
                            FieldHeight = bounds.Height
                        });
                    }
                    catch
                    {
                        // Campo individual no procesable — continuar
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Datos de un campo AcroForm de texto con valor y coordenadas en puntos PDF.
        /// Solo se usa durante la inyección de texto — no se persiste.
        /// </summary>
        private sealed class AcroFieldInfo
        {
            public string Value { get; set; } = string.Empty;
            public int PageNumber { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double FieldWidth { get; set; }
            public double FieldHeight { get; set; }
        }

    }
}