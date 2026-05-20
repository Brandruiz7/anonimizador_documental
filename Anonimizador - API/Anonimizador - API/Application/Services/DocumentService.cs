using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Repositories;
using Anonimizador___API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;

namespace Anonimizador___API.Application.Services
{
    /// <summary>
    /// Service responsible for document upload and anonymization processing.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repository;
        private readonly IAnonymizationService _anonymizationService;
        private readonly ILogger<DocumentService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentService"/> class.
        /// </summary>
        /// <param name="repository">Document repository.</param>
        /// <param name="anonymizationService">Anonymization service.</param>
        /// <param name="logger">Logger service.</param>
        public DocumentService(
            IDocumentRepository repository,
            IAnonymizationService anonymizationService,
            ILogger<DocumentService> logger)
        {
            _repository = repository;
            _anonymizationService = anonymizationService;
            _logger = logger;
        }

        /// <summary>
        /// Versión con streaming seguro — minimiza copias en memoria.
        /// Retorna un Stream listo para enviarse al cliente sin acumular el archivo completo.
        /// </summary>
        public async Task<(Stream FileStream, string FileName, string ContentType)> UploadStreamAsync(
            UploadDocumentRequestDto request)
        {
            try
            {
                _logger.LogInformation("Starting streaming anonymization process");

                // =========================
                // 1. VALIDACIONES
                // =========================
                var file = request.File
                    ?? throw new Exception("File is null");

                if (file.Length == 0)
                    throw new Exception("File is empty");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".docx")
                    throw new Exception("Invalid file extension");

                if (file.ContentType != "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
                    throw new Exception("Invalid content type");

                if (file.Length > 104857600)
                    throw new Exception("File exceeds maximum allowed size");

                // =========================
                // 2. CARGAR EN UN SOLO STREAM
                // Copiamos directo al stream de trabajo
                // sin crear un byte[] intermedio
                // =========================
                var workStream = new MemoryStream();

                await file.OpenReadStream().CopyToAsync(workStream);

                workStream.Position = 0;

                _logger.LogInformation("File loaded into work stream");

                // =========================
                // 3. VALIDAR ESTRUCTURA DOCX
                // =========================
                try
                {
                    using var doc = WordprocessingDocument.Open(workStream, false);
                    workStream.Position = 0;
                }
                catch
                {
                    await workStream.DisposeAsync();
                    throw new Exception("Invalid Word document");
                }

                // =========================
                // 4. HASH ORIGINAL
                // Calculamos sobre el stream sin crear byte[]
                // =========================
                string originalHash;

                using (var sha256 = SHA256.Create())
                {
                    workStream.Position = 0;
                    var hashBytes = await sha256.ComputeHashAsync(workStream);
                    originalHash = BitConverter.ToString(hashBytes).Replace("-", "");
                    workStream.Position = 0;
                }

                _logger.LogInformation("Original hash generated");

                // =========================
                // 5. REGISTRAR PROCESO
                // =========================
                var processId = await _repository.InsertDocumentProcessAsync(
                    file.FileName,
                    file.ContentType,
                    file.Length / 1024,
                    originalHash,
                    request.UploadedBy);

                _logger.LogInformation("Process registered. ProcessId: {ProcessId}", processId);

                // =========================
                // 6. CONSTRUIR TARGETS
                // =========================

                // Validamos que venga al menos una persona
                if (request.Persons == null || request.Persons.Count == 0)
                    throw new Exception("At least one person must be provided.");

                // Mapeamos cada PersonTargetDto a AnonymizationTargetDto
                // y filtramos personas que no tengan ningún campo lleno
                var targets = request.Persons
                    .Where(p =>
                        !string.IsNullOrWhiteSpace(p.FullName) ||
                        !string.IsNullOrWhiteSpace(p.Identification) ||
                        !string.IsNullOrWhiteSpace(p.Email) ||
                        !string.IsNullOrWhiteSpace(p.PhoneNumber) ||
                        !string.IsNullOrWhiteSpace(p.Position) ||
                        !string.IsNullOrWhiteSpace(p.Address))
                    .Select(p => new AnonymizationTargetDto
                    {
                        FullName = p.FullName,
                        Identification = p.Identification,
                        Email = p.Email,
                        PhoneNumber = p.PhoneNumber,
                        Position = p.Position,
                        Address = p.Address
                    })
                    .ToList();

                if (targets.Count == 0)
                    throw new Exception("No anonymization targets provided. Fill at least one field per person.");

                _logger.LogInformation(
                    "Anonymization targets built: {Count} person(s)", targets.Count);

                // =========================
                // 7. ANONIMIZAR
                // Pasamos los bytes del workStream directamente
                // =========================
                workStream.Position = 0;
                var fileBytes = workStream.ToArray();

                // Liberamos el workStream — ya no lo necesitamos
                await workStream.DisposeAsync();

                var anonymizationResult = await _anonymizationService.AnonymizeAsync(
                    fileBytes,
                    targets);

                // Limpiamos el array original apenas terminamos
                Array.Clear(fileBytes, 0, fileBytes.Length);

                _logger.LogInformation(
                    "Document anonymized. Fields replaced: {Count}",
                    anonymizationResult.AuditFields.Count);

                // =========================
                // 8. HASH ANONIMIZADO + VERSIÓN
                // =========================
                string anonymizedHash;

                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(anonymizationResult.FileBytes);
                    anonymizedHash = BitConverter.ToString(hashBytes).Replace("-", "");
                }

                var versionId = await _repository.InsertDocumentVersionAsync(
                    processId,
                    "ANONYMIZED",
                    anonymizedHash);

                _logger.LogInformation(
                    "Version registered. VersionId: {VersionId}", versionId);

                // =========================
                // 9. AUDITORÍA
                // =========================
                foreach (var field in anonymizationResult.AuditFields)
                {
                    await _repository.InsertAuditFieldAsync(
                        versionId,
                        field.FieldType,
                        field.OriginalValue,
                        field.AnonymizedValue);
                }

                _logger.LogInformation(
                    "Audit fields registered: {Count}", anonymizationResult.AuditFields.Count);

                // =========================
                // 10. ACTUALIZAR ESTADO
                // =========================
                await _repository.UpdateProcessStatusAsync(processId, 3);

                _logger.LogInformation("Process status updated to ANONYMIZED");

                // =========================
                // 11. RETORNAR STREAM
                // Convertimos los bytes anonimizados a stream
                // y los bytes se liberan cuando el stream se cierra
                // =========================
                var resultStream = new MemoryStream(anonymizationResult.FileBytes);
                resultStream.Position = 0;

                return (
                    resultStream,
                    $"ANONYMIZED_{file.FileName}",
                    file.ContentType
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during streaming anonymization");
                throw;
            }
        }

        /// <summary>
        /// Retorna el historial de documentos procesados para el dashboard.
        /// </summary>
        public async Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync()
        {
            _logger.LogInformation("Fetching document history for dashboard");
            return await _repository.GetAllDocumentsAsync();
        }

        /// <summary>
        /// Retorna las métricas para el dashboard.
        /// </summary>
        public async Task<MetricsResponseDto> GetMetricsAsync()
        {
            _logger.LogInformation("Fetching metrics for dashboard");
            return await _repository.GetMetricsAsync();
        }
    }
}