using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;
using Anonimizador___API.Interfaces.Repositories;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;

namespace Anonimizador___API.Application.Services.Documents
{
    /// <summary>
    /// Servicio principal de documentos.
    /// Orquesta la validación, el procesamiento y la auditoría del flujo de anonimización.
    /// </summary>
    public class DocumentService : IDocumentService
    {
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
            try
            {
                _logger.LogInformation("Iniciando proceso de anonimización");

                // 1. Validaciones básicas del archivo
                var file = request.File
                    ?? throw new Exception("El archivo es nulo.");

                if (file.Length == 0)
                    throw new Exception("El archivo está vacío.");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                var allowedExtensions = new[] { ".docx", ".pdf" };
                if (!allowedExtensions.Contains(extension))
                    throw new Exception("Solo se permiten archivos .docx y .pdf.");

                var allowedContentTypes = new[]
                {
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/pdf"
                };

                if (!allowedContentTypes.Contains(file.ContentType))
                    throw new Exception("Tipo de contenido no permitido.");

                if (file.Length > 104857600)
                    throw new Exception("El archivo supera el tamaño máximo permitido (100 MB).");

                // 2. Cargar archivo en memoria
                var workStream = new MemoryStream();
                await file.OpenReadStream().CopyToAsync(workStream);
                workStream.Position = 0;

                _logger.LogInformation("Archivo cargado en memoria");

                // 3. Validar estructura del documento
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
                            throw new Exception("El archivo PDF no es válido.");
                    }

                    workStream.Position = 0;
                }
                catch
                {
                    await workStream.DisposeAsync();
                    throw new Exception($"Estructura de documento inválida para extensión {extension}.");
                }

                // 4. Calcular hash del archivo original
                string originalHash;
                using (var sha256 = SHA256.Create())
                {
                    workStream.Position = 0;
                    var hashBytes = await sha256.ComputeHashAsync(workStream);
                    originalHash = BitConverter.ToString(hashBytes).Replace("-", "");
                    workStream.Position = 0;
                }

                // 5. Registrar proceso en BD
                var processId = await _repository.InsertDocumentProcessAsync(
                    file.FileName,
                    file.ContentType,
                    file.Length / 1024,
                    originalHash,
                    request.UploadedBy);

                // 6. Construir targets de anonimización
                if (request.Persons == null || request.Persons.Count == 0)
                    throw new Exception("Debe proporcionar al menos una persona.");

                var targets = new List<AnonymizationTargetDto>();

                // Datos generales del documento — índice -1
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

                // Personas
                for (int i = 0; i < request.Persons.Count; i++)
                {
                    var p = request.Persons[i];

                    if (string.IsNullOrWhiteSpace(p.FullName) &&
                        string.IsNullOrWhiteSpace(p.Identification) &&
                        string.IsNullOrWhiteSpace(p.Email) &&
                        string.IsNullOrWhiteSpace(p.PhoneNumber) &&
                        string.IsNullOrWhiteSpace(p.Position) &&
                        string.IsNullOrWhiteSpace(p.Address) &&
                        string.IsNullOrWhiteSpace(p.Institution) &&
                        string.IsNullOrWhiteSpace(p.BankAccount) &&
                        string.IsNullOrWhiteSpace(p.MedicalCondition) &&
                        string.IsNullOrWhiteSpace(p.FreeText))
                        continue;

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

                    // Variaciones del nombre
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

                if (targets.Count == 0)
                    throw new Exception("No se proporcionaron datos válidos para anonimizar.");

                // 7. Seleccionar procesador y anonimizar
                workStream.Position = 0;
                var fileBytes = workStream.ToArray();
                await workStream.DisposeAsync();

                var processor = _processors.FirstOrDefault(p => p.CanProcess(extension))
                    ?? throw new Exception($"No hay procesador disponible para {extension}.");

                var result = await processor.ProcessAsync(fileBytes, targets);

                Array.Clear(fileBytes, 0, fileBytes.Length);

                // 8. Calcular hash del resultado y registrar versión
                string anonymizedHash;
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(result.FileBytes);
                    anonymizedHash = BitConverter.ToString(hashBytes).Replace("-", "");
                }

                var versionId = await _repository.InsertDocumentVersionAsync(
                    processId, "ANONYMIZED", anonymizedHash);

                // 9. Registrar auditoría de campos
                foreach (var field in result.AuditFields)
                {
                    await _repository.InsertAuditFieldAsync(
                        versionId,
                        field.FieldType,
                        field.OriginalValue,
                        field.AnonymizedValue);
                }

                // 10. Actualizar estado a ANONYMIZED
                await _repository.UpdateProcessStatusAsync(processId, 3);

                _logger.LogInformation(
                    "Anonimización completada. Campos reemplazados: {Count}",
                    result.AuditFields.Count);

                // 11. Retornar stream del resultado
                var resultStream = new MemoryStream(result.FileBytes);
                resultStream.Position = 0;

                return (resultStream, $"ANONYMIZED_{file.FileName}", file.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la anonimización");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync()
            => await _repository.GetAllDocumentsAsync();

        /// <inheritdoc />
        public async Task<MetricsResponseDto> GetMetricsAsync()
            => await _repository.GetMetricsAsync();
    }
}